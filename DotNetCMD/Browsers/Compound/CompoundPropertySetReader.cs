using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetCommander
{
    internal sealed class CompoundPropertySetRow
    {
        public CompoundPropertySetRow(int propertyId, string name, string value, string typeName)
        {
            PropertyId = propertyId;
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            TypeName = typeName ?? string.Empty;
        }

        public int PropertyId { get; }
        public string Name { get; }
        public string Value { get; }
        public string TypeName { get; }
    }

    /// <summary>
    /// Reads the small, common scalar subset of OLE Property Set streams. Complex values
    /// are surfaced as unsupported rows; malformed lengths and offsets are rejected.
    /// </summary>
    internal static class CompoundPropertySetReader
    {
        private const int MaximumStreamBytes = 8 * 1024 * 1024;
        private const int MaximumProperties = 1024;
        private const int MaximumStringCharacters = 1024 * 1024;
        private const int PropertySetHeaderSize = 28;
        private const ushort LittleEndianByteOrder = 0xFFFE;
        private const ushort MinimumSupportedVersion = 0;
        private const ushort MaximumSupportedVersion = 1;
        private const int SummaryInformationCodePagePropertyId = 1;

        private static readonly Guid SummaryInformationFormatId = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9");
        private static readonly Guid DocumentSummaryInformationFormatId = new Guid("D5CDD502-2E9C-101B-9397-08002B2CF9AE");
        private static readonly Guid UserDefinedFormatId = new Guid("D5CDD505-2E9C-101B-9397-08002B2CF9AE");

        public static IReadOnlyList<CompoundPropertySetRow> Read(string streamName, byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaximumStreamBytes)
                throw Invalid("The property set exceeds the parser size limit.");
            if (bytes.Length < PropertySetHeaderSize)
                throw Invalid("The property set header is truncated.");

            ushort byteOrder = ReadUInt16(bytes, 0, bytes.Length);
            ushort version = ReadUInt16(bytes, 2, bytes.Length);
            if (byteOrder != LittleEndianByteOrder)
                throw Invalid("The property set byte order is unsupported.");
            if (version < MinimumSupportedVersion || version > MaximumSupportedVersion)
                throw Invalid("The property set version is unsupported.");

            uint rawSetCount = ReadUInt32(bytes, 24, bytes.Length);
            if (rawSetCount == 0 || rawSetCount > 2)
                throw Invalid("The property set count is invalid.");

            int headerEnd = checked(PropertySetHeaderSize + (int)rawSetCount * 20);
            RequireRange(bytes.Length, 0, headerEnd);

            var sections = new List<PropertySection>((int)rawSetCount);
            var sectionOffsets = new HashSet<uint>();
            for (int index = 0; index < rawSetCount; index++)
            {
                int descriptorOffset = PropertySetHeaderSize + index * 20;
                Guid formatId = new Guid(ReadBytes(bytes, descriptorOffset, 16, bytes.Length));
                uint sectionOffset = ReadUInt32(bytes, descriptorOffset + 16, bytes.Length);
                if ((sectionOffset & 3) != 0 || sectionOffset < headerEnd || sectionOffset > int.MaxValue)
                    throw Invalid("A property section offset is invalid or unaligned.");
                if (!sectionOffsets.Add(sectionOffset))
                    throw Invalid("Property sections overlap.");

                sections.Add(ReadSection(bytes, (int)sectionOffset, formatId));
            }

            if (sections.Sum(section => section.Properties.Count) > MaximumProperties)
                throw Invalid("The total property count exceeds the parser limit.");

            ValidateSectionsDoNotOverlap(sections, bytes.Length);
            ValidateExpectedStreamFormat(streamName, sections);

            var rows = new List<CompoundPropertySetRow>();
            foreach (PropertySection section in sections)
            {
                Encoding ansiEncoding = ResolveEncoding(bytes, section);
                foreach (PropertyRecord property in section.Properties)
                {
                    string sectionName = GetSectionName(section.FormatId);
                    string propertyName = GetPropertyName(section.FormatId, property.Id);
                    if (property.Id == SummaryInformationCodePagePropertyId)
                        propertyName = "Code Page";
                    string fullName = string.IsNullOrEmpty(sectionName)
                        ? propertyName
                        : sectionName + " — " + propertyName;

                    (string value, string typeName) = FormatProperty(bytes, section, property, ansiEncoding);
                    if (property.Id > int.MaxValue)
                        throw Invalid("A property ID is outside the supported range.");
                    rows.Add(new CompoundPropertySetRow((int)property.Id, fullName, value, typeName));
                }
            }

            return rows;
        }

        private static PropertySection ReadSection(byte[] bytes, int offset, Guid formatId)
        {
            RequireRange(bytes.Length, offset, 8);
            uint rawSize = ReadUInt32(bytes, offset, bytes.Length);
            uint rawCount = ReadUInt32(bytes, offset + 4, bytes.Length);
            if (rawSize < 8 || rawSize > int.MaxValue || rawSize > bytes.Length - offset)
                throw Invalid("A property section size is invalid.");
            if (rawCount > MaximumProperties)
                throw Invalid("The property count exceeds the parser limit.");

            int size = (int)rawSize;
            int tableEnd;
            try
            {
                tableEnd = checked(8 + (int)rawCount * 8);
            }
            catch (OverflowException)
            {
                throw Invalid("The property table size is invalid.");
            }
            if (tableEnd > size)
                throw Invalid("The property table is truncated.");

            var properties = new List<PropertyRecord>((int)rawCount);
            var ids = new HashSet<uint>();
            var offsets = new HashSet<uint>();
            for (int index = 0; index < rawCount; index++)
            {
                int recordOffset = checked(offset + 8 + index * 8);
                uint id = ReadUInt32(bytes, recordOffset, offset + size);
                uint valueOffset = ReadUInt32(bytes, recordOffset + 4, offset + size);
                if (!ids.Add(id))
                    throw Invalid("The property table contains duplicate property IDs.");
                if ((valueOffset & 3) != 0 || valueOffset < tableEnd || valueOffset >= size)
                    throw Invalid("A property value offset is invalid or unaligned.");
                if (!offsets.Add(valueOffset))
                    throw Invalid("The property table contains duplicate value offsets.");
                properties.Add(new PropertyRecord(id, valueOffset));
            }

            properties.Sort((left, right) => left.Offset.CompareTo(right.Offset));
            for (int index = 0; index < properties.Count; index++)
            {
                int end = index + 1 < properties.Count ? (int)properties[index + 1].Offset : size;
                properties[index].ValueEnd = checked((uint)end);
            }

            return new PropertySection(formatId, offset, size, properties);
        }

        private static void ValidateSectionsDoNotOverlap(IReadOnlyList<PropertySection> sections, int totalLength)
        {
            PropertySection[] ordered = sections.OrderBy(section => section.Offset).ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                int end = checked(ordered[index].Offset + ordered[index].Size);
                if (end > totalLength)
                    throw Invalid("A property section extends past the stream.");
                if (index + 1 < ordered.Length && end > ordered[index + 1].Offset)
                    throw Invalid("Property sections overlap.");
            }
        }

        private static Encoding ResolveEncoding(byte[] bytes, PropertySection section)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            PropertyRecord codePageProperty = section.Properties.FirstOrDefault(property => property.Id == SummaryInformationCodePagePropertyId);
            if (codePageProperty == null)
                return Encoding.Latin1;

            try
            {
                int valueOffset = checked(section.Offset + (int)codePageProperty.Offset);
                int valueEnd = checked(section.Offset + (int)codePageProperty.ValueEnd);
                uint variantType = ReadUInt32(bytes, valueOffset, valueEnd);
                if (variantType == 2)
                {
                    ushort codePage = ReadUInt16(bytes, valueOffset + 4, valueEnd);
                    return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                }
                if (variantType == 19)
                {
                    uint codePage = ReadUInt32(bytes, valueOffset + 4, valueEnd);
                    if (codePage <= int.MaxValue)
                        return Encoding.GetEncoding((int)codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is NotSupportedException)
            {
                // A missing or unsupported code page must not make otherwise safe fields unreadable.
            }

            return Encoding.Latin1;
        }

        private static (string Value, string TypeName) FormatProperty(
            byte[] bytes,
            PropertySection section,
            PropertyRecord property,
            Encoding ansiEncoding)
        {
            int sectionAbsoluteOffset = section.Offset + (int)property.Offset;
            int available = (int)property.ValueEnd - (int)property.Offset;
            RequireRange(bytes.Length, sectionAbsoluteOffset, Math.Min(available, 4));
            uint variantType = ReadUInt32(bytes, sectionAbsoluteOffset, section.Offset + (int)property.ValueEnd);

            switch (variantType)
            {
                case 2: // VT_I2
                    return (ReadInt16Value(bytes, sectionAbsoluteOffset + 4, section, property).ToString(CultureInfo.InvariantCulture), "I2");
                case 3: // VT_I4
                    return (ReadInt32Value(bytes, sectionAbsoluteOffset + 4, section, property).ToString(CultureInfo.InvariantCulture), "I4");
                case 19: // VT_UI4
                    return (ReadUInt32Value(bytes, sectionAbsoluteOffset + 4, section, property).ToString(CultureInfo.InvariantCulture), "UI4");
                case 20: // VT_I8
                    return (ReadInt64Value(bytes, sectionAbsoluteOffset + 4, section, property).ToString(CultureInfo.InvariantCulture), "I8");
                case 21: // VT_UI8
                    return (ReadUInt64Value(bytes, sectionAbsoluteOffset + 4, section, property).ToString(CultureInfo.InvariantCulture), "UI8");
                case 5: // VT_R8
                    return (ReadDoubleValue(bytes, sectionAbsoluteOffset + 4, section, property).ToString("R", CultureInfo.InvariantCulture), "R8");
                case 11: // VT_BOOL
                    short boolean = ReadInt16Value(bytes, sectionAbsoluteOffset + 4, section, property);
                    return (boolean == 0 ? "False" : boolean == -1 ? "True" : "BOOL(" + boolean.ToString(CultureInfo.InvariantCulture) + ")", "BOOL");
                case 64: // VT_FILETIME
                    long fileTime = ReadInt64Value(bytes, sectionAbsoluteOffset + 4, section, property);
                    try
                    {
                        return (DateTime.FromFileTimeUtc(fileTime).ToString("O", CultureInfo.InvariantCulture), "FILETIME");
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw Invalid("A FILETIME property value is invalid.");
                    }
                case 30: // VT_LPSTR
                    return (ReadAnsiString(bytes, sectionAbsoluteOffset, section, property, ansiEncoding), "LPSTR");
                case 31: // VT_LPWSTR
                    return (ReadUnicodeString(bytes, sectionAbsoluteOffset, section, property), "LPWSTR");
                default:
                    return ("Unsupported property type (0x" + variantType.ToString("X8", CultureInfo.InvariantCulture) + ")", "VT 0x" + variantType.ToString("X8", CultureInfo.InvariantCulture));
            }
        }

        private static string ReadAnsiString(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property, Encoding encoding)
        {
            int end = section.Offset + (int)property.ValueEnd;
            RequireValueRange(valueOffset, 8, end);
            uint rawCount = ReadUInt32(bytes, valueOffset + 4, end);
            if (rawCount > MaximumStringCharacters || rawCount > int.MaxValue)
                throw Invalid("An LPSTR property length exceeds the parser limit.");
            int count = (int)rawCount;
            RequireValueRange(valueOffset, checked(8 + count), end);
            try
            {
                string result = encoding.GetString(bytes, valueOffset + 8, count);
                return result.TrimEnd('\0');
            }
            catch (DecoderFallbackException)
            {
                return "Invalid " + encoding.WebName + " text";
            }
        }

        private static string ReadUnicodeString(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            int end = section.Offset + (int)property.ValueEnd;
            RequireValueRange(valueOffset, 8, end);
            uint rawCount = ReadUInt32(bytes, valueOffset + 4, end);
            if (rawCount > MaximumStringCharacters || rawCount > int.MaxValue)
                throw Invalid("An LPWSTR property length exceeds the parser limit.");
            int count = (int)rawCount;
            int byteCount;
            try
            {
                byteCount = checked(count * 2);
                RequireValueRange(valueOffset, checked(8 + byteCount), end);
                string result = Encoding.Unicode.GetString(bytes, valueOffset + 8, byteCount);
                return result.TrimEnd('\0');
            }
            catch (OverflowException)
            {
                throw Invalid("An LPWSTR property length is invalid.");
            }
        }

        private static short ReadInt16Value(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            return unchecked((short)ReadUnsigned(bytes, valueOffset, 2, section, property));
        }

        private static int ReadInt32Value(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            return unchecked((int)ReadUnsigned(bytes, valueOffset, 4, section, property));
        }

        private static uint ReadUInt32Value(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            return (uint)ReadUnsigned(bytes, valueOffset, 4, section, property);
        }

        private static long ReadInt64Value(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            return unchecked((long)ReadUnsigned(bytes, valueOffset, 8, section, property));
        }

        private static ulong ReadUInt64Value(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            return ReadUnsigned(bytes, valueOffset, 8, section, property);
        }

        private static double ReadDoubleValue(byte[] bytes, int valueOffset, PropertySection section, PropertyRecord property)
        {
            ulong bits = ReadUnsigned(bytes, valueOffset, 8, section, property);
            return BitConverter.Int64BitsToDouble(unchecked((long)bits));
        }

        private static ulong ReadUnsigned(byte[] bytes, int valueOffset, int count, PropertySection section, PropertyRecord property)
        {
            int end = section.Offset + (int)property.ValueEnd;
            RequireValueRange(valueOffset, count, end);
            ulong result = 0;
            for (int index = 0; index < count; index++)
                result |= (ulong)bytes[valueOffset + index] << (index * 8);
            return result;
        }

        private static string GetSectionName(Guid formatId)
        {
            if (formatId == SummaryInformationFormatId)
                return "Summary Information";
            if (formatId == DocumentSummaryInformationFormatId)
                return "Document Summary Information";
            if (formatId == UserDefinedFormatId)
                return "User Defined Properties";
            return "Property Set " + formatId.ToString("B").ToUpperInvariant();
        }

        private static void ValidateExpectedStreamFormat(string streamName, IReadOnlyList<PropertySection> sections)
        {
            if (string.Equals(streamName, "\u0005SummaryInformation", StringComparison.OrdinalIgnoreCase))
            {
                if (sections.Count != 1 || sections[0].FormatId != SummaryInformationFormatId)
                    throw Invalid("The SummaryInformation stream contains an unexpected property set.");
                return;
            }

            if (string.Equals(streamName, "\u0005DocumentSummaryInformation", StringComparison.OrdinalIgnoreCase))
            {
                bool hasDocumentSummary = false;
                bool hasUserDefined = false;
                foreach (PropertySection section in sections)
                {
                    if (section.FormatId == DocumentSummaryInformationFormatId && !hasDocumentSummary)
                        hasDocumentSummary = true;
                    else if (section.FormatId == UserDefinedFormatId && !hasUserDefined)
                        hasUserDefined = true;
                    else
                        throw Invalid("The DocumentSummaryInformation stream contains an unexpected property set.");
                }

                if (!hasDocumentSummary)
                    throw Invalid("The DocumentSummaryInformation section is missing.");
            }
        }

        private static string GetPropertyName(Guid formatId, uint propertyId)
        {
            if (formatId == SummaryInformationFormatId)
            {
                return propertyId switch
                {
                    0 => "Dictionary",
                    1 => "Code Page",
                    2 => "Title",
                    3 => "Subject",
                    4 => "Author",
                    5 => "Keywords",
                    6 => "Comments",
                    7 => "Template",
                    8 => "Last Saved By",
                    9 => "Revision Number",
                    10 => "Total Editing Time",
                    11 => "Last Printed",
                    12 => "Created",
                    13 => "Last Saved",
                    14 => "Page Count",
                    15 => "Word Count",
                    16 => "Character Count",
                    17 => "Thumbnail",
                    18 => "Application Name",
                    19 => "Security",
                    _ => "Property " + propertyId.ToString(CultureInfo.InvariantCulture)
                };
            }

            if (formatId == DocumentSummaryInformationFormatId)
            {
                return propertyId switch
                {
                    0 => "Dictionary",
                    1 => "Code Page",
                    2 => "Category",
                    3 => "Presentation Target",
                    4 => "Bytes",
                    5 => "Lines",
                    6 => "Paragraphs",
                    7 => "Slides",
                    8 => "Notes",
                    9 => "Hidden Slides",
                    10 => "Multimedia Clips",
                    11 => "Scale Crop",
                    12 => "Heading Pairs",
                    13 => "Titles of Parts",
                    14 => "Manager",
                    15 => "Company",
                    16 => "Links Up To Date",
                    17 => "Character Count With Spaces",
                    18 => "Shared Document",
                    19 => "Link Base",
                    20 => "Hyperlink List",
                    21 => "Version",
                    _ => "Property " + propertyId.ToString(CultureInfo.InvariantCulture)
                };
            }

            return "Property " + propertyId.ToString(CultureInfo.InvariantCulture);
        }

        private static uint ReadUInt32(byte[] bytes, int offset, int end)
        {
            RequireValueRange(offset, 4, end);
            return (uint)(bytes[offset]
                | bytes[offset + 1] << 8
                | bytes[offset + 2] << 16
                | bytes[offset + 3] << 24);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset, int end)
        {
            RequireValueRange(offset, 2, end);
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        private static byte[] ReadBytes(byte[] bytes, int offset, int count, int end)
        {
            RequireValueRange(offset, count, end);
            var result = new byte[count];
            Buffer.BlockCopy(bytes, offset, result, 0, count);
            return result;
        }

        private static void RequireRange(int totalLength, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > totalLength - count)
                throw Invalid("The property set contains an out-of-range offset or length.");
        }

        private static void RequireValueRange(int offset, int count, int end)
        {
            if (offset < 0 || count < 0 || end < 0 || offset > end - count)
                throw Invalid("A property value is truncated or out of range.");
        }

        private static InvalidDataException Invalid(string message) => new InvalidDataException(message);

        private sealed class PropertySection
        {
            public PropertySection(Guid formatId, int offset, int size, List<PropertyRecord> properties)
            {
                FormatId = formatId;
                Offset = offset;
                Size = size;
                Properties = properties;
            }

            public Guid FormatId { get; }
            public int Offset { get; }
            public int Size { get; }
            public List<PropertyRecord> Properties { get; }
        }

        private sealed class PropertyRecord
        {
            public PropertyRecord(uint id, uint offset)
            {
                Id = id;
                Offset = offset;
            }

            public uint Id { get; }
            public uint Offset { get; }
            public uint ValueEnd { get; set; }
        }
    }
}
