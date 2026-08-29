using OpenMcdf;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    /// <summary>
    /// Extracts the main story text from a Word 97-2007 binary document.
    /// Formatting and secondary stories intentionally remain outside Quick View scope.
    /// </summary>
    internal static class WordBinaryTextExtractor
    {
        private const ushort WordBinaryIdent = 0xA5EC;
        private const int FibBaseSize = 32;
        // FibRgFcLcb97.fcClx/lcbClx are the 34th offset/length pair.
        // In a Word 97 FIB the pair starts at WordDocument offset 0x01A2.
        private const int ClxPairIndex = 33;
        private const int MaximumClxBytes = 64 * 1024 * 1024;
        private const int MaximumPreviewCharacters = 4 * 1024 * 1024;
        private static readonly Encoding UnicodeEncoding = new UnicodeEncoding(false, false, true);

        public static Task<string> TryExtractMainTextAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !FileTypeClassifier.IsCompoundFile(path))
                return Task.FromResult<string>(null);

            return Task.Run(() => TryExtractMainText(path, cancellationToken), cancellationToken);
        }

        private static string TryExtractMainText(string path, CancellationToken cancellationToken)
        {
            try
            {
                using RootStorage root = RootStorage.OpenRead(path, StorageModeFlags.StrictValidation);
                if (!HasRootStream(root, "WordDocument"))
                    return null;
                using CfbStream wordDocument = root.OpenStream("WordDocument");
                FibTextInfo fib = ReadFib(wordDocument);
                if (fib.IsEncrypted)
                    return null;
                if (fib.MainTextCharacters == 0)
                    return string.Empty;

                string tableName = fib.UseOneTable ? "1Table" : "0Table";
                if (!HasRootStream(root, tableName))
                    return null;
                using CfbStream tableStream = root.OpenStream(tableName);
                byte[] clx = ReadRange(tableStream, fib.ClxOffset, fib.ClxLength, MaximumClxBytes);
                return ExtractPieceTableText(wordDocument, clx, fib.MainTextCharacters, cancellationToken);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is InvalidDataException ||
                ex is FormatException ||
                ex is ArgumentException ||
                ex is NotSupportedException ||
                ex is EndOfStreamException ||
                ex is OverflowException ||
                ex is UnauthorizedAccessException)
            {
                LogService.LogException("WordBinaryTextExtractor.TryExtractMainText", ex);
                return null;
            }
        }

        private static bool HasRootStream(RootStorage root, string name)
        {
            foreach (EntryInfo entry in root.EnumerateEntries())
            {
                if (entry.Type == EntryType.Stream &&
                    string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static FibTextInfo ReadFib(CfbStream stream)
        {
            byte[] fibBase = ReadRange(stream, 0, FibBaseSize, FibBaseSize);
            if (ReadUInt16(fibBase, 0) != WordBinaryIdent)
                throw new InvalidDataException("The WordDocument stream does not contain a Word binary FIB.");

            ushort flags = ReadUInt16(fibBase, 10);
            bool encrypted = (flags & 0x0100) != 0;
            bool useOneTable = (flags & 0x0200) != 0;

            long offset = FibBaseSize;
            ushort csw = ReadUInt16(ReadRange(stream, offset, 2, 2), 0);
            offset = CheckedAdvance(offset, 2L + csw * 2L, stream.Length);

            ushort cslw = ReadUInt16(ReadRange(stream, offset, 2, 2), 0);
            offset = CheckedAdvance(offset, 2, stream.Length);
            if (cslw < 4)
                throw new InvalidDataException("The Word FIB does not contain ccpText.");
            byte[] fibRgLw = ReadRange(stream, offset, checked(cslw * 4), 4096);
            int mainTextCharacters = ReadInt32(fibRgLw, 12);
            if (mainTextCharacters < 0)
                throw new InvalidDataException("The Word FIB contains an invalid main text length.");
            offset = CheckedAdvance(offset, cslw * 4L, stream.Length);

            ushort cbRgFcLcb = ReadUInt16(ReadRange(stream, offset, 2, 2), 0);
            offset = CheckedAdvance(offset, 2, stream.Length);
            if (cbRgFcLcb <= ClxPairIndex)
                throw new InvalidDataException("The Word FIB does not contain a CLX location.");

            long clxPairOffset = CheckedAdvance(offset, ClxPairIndex * 8L, stream.Length);
            byte[] clxPair = ReadRange(stream, clxPairOffset, 8, 8);
            uint clxOffset = ReadUInt32(clxPair, 0);
            uint clxLength = ReadUInt32(clxPair, 4);
            if (clxLength == 0 || clxLength > MaximumClxBytes)
                throw new InvalidDataException("The Word CLX size is invalid or too large for Quick View.");

            return new FibTextInfo(useOneTable, encrypted, mainTextCharacters, clxOffset, checked((int)clxLength));
        }

        private static string ExtractPieceTableText(
            CfbStream wordDocument,
            byte[] clx,
            int mainTextCharacters,
            CancellationToken cancellationToken)
        {
            int pcdtOffset = FindPcdtOffset(clx);
            uint plcSizeValue = ReadUInt32(clx, pcdtOffset + 1);
            if (plcSizeValue > int.MaxValue)
                throw new InvalidDataException("The Word piece table is too large.");
            int plcSize = (int)plcSizeValue;
            int plcOffset = checked(pcdtOffset + 5);
            if (plcSize < 4 || (plcSize - 4) % 12 != 0 || plcOffset > clx.Length - plcSize)
                throw new InvalidDataException("The Word piece table has an invalid size.");

            int pieceCount = (plcSize - 4) / 12;
            int cpArrayBytes = checked((pieceCount + 1) * 4);
            int pcdArrayOffset = checked(plcOffset + cpArrayBytes);
            int characterLimit = Math.Min(mainTextCharacters, MaximumPreviewCharacters);
            var result = new StringBuilder(Math.Min(characterLimit, 256 * 1024));
            int previousCp = -1;

            for (int index = 0; index < pieceCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int cpStart = ReadInt32(clx, plcOffset + index * 4);
                int cpEnd = ReadInt32(clx, plcOffset + (index + 1) * 4);
                if (cpStart < 0 || cpEnd <= cpStart || cpStart < previousCp)
                    throw new InvalidDataException("The Word piece table contains invalid character positions.");
                previousCp = cpStart;
                if (cpStart >= characterLimit)
                    break;

                int clippedEnd = Math.Min(cpEnd, characterLimit);
                int characterCount = clippedEnd - cpStart;
                if (characterCount <= 0)
                    continue;

                int pcdOffset = checked(pcdArrayOffset + index * 8);
                uint fcCompressed = ReadUInt32(clx, pcdOffset + 2);
                bool compressed = (fcCompressed & 0x40000000U) != 0;
                long storedOffset = fcCompressed & 0x3FFFFFFFU;
                long textOffset = compressed ? storedOffset / 2 : storedOffset;
                int byteCount = checked(characterCount * (compressed ? 1 : 2));
                byte[] bytes = ReadRange(wordDocument, textOffset, byteCount, MaximumPreviewCharacters * 2);
                AppendReadableText(result, bytes, compressed);
            }

            if (mainTextCharacters > MaximumPreviewCharacters)
            {
                result.AppendLine();
                result.AppendLine();
                result.Append("[Quick View truncated]");
            }

            return result.ToString().TrimEnd('\0');
        }

        private static int FindPcdtOffset(byte[] clx)
        {
            int offset = 0;
            while (offset < clx.Length && clx[offset] == 0x01)
            {
                if (offset > clx.Length - 3)
                    throw new InvalidDataException("The Word CLX contains a truncated Prc.");
                ushort grpprlLength = ReadUInt16(clx, offset + 1);
                offset = checked(offset + 3 + grpprlLength);
            }

            if (offset >= clx.Length || clx[offset] != 0x02)
                throw new InvalidDataException("The Word CLX does not contain a Pcdt.");
            return offset;
        }

        private static void AppendReadableText(StringBuilder target, byte[] bytes, bool compressed)
        {
            if (compressed)
            {
                foreach (byte value in bytes)
                    AppendReadableCharacter(target, DecodeCompressedCharacter(value));
                return;
            }

            string text = UnicodeEncoding.GetString(bytes);
            foreach (char value in text)
                AppendReadableCharacter(target, value);
        }

        private static char DecodeCompressedCharacter(byte value)
        {
            return value switch
            {
                0x82 => '\u201A', 0x83 => '\u0192', 0x84 => '\u201E', 0x85 => '\u2026',
                0x86 => '\u2020', 0x87 => '\u2021', 0x88 => '\u02C6', 0x89 => '\u2030',
                0x8A => '\u0160', 0x8B => '\u2039', 0x8C => '\u0152', 0x91 => '\u2018',
                0x92 => '\u2019', 0x93 => '\u201C', 0x94 => '\u201D', 0x95 => '\u2022',
                0x96 => '\u2013', 0x97 => '\u2014', 0x98 => '\u02DC', 0x99 => '\u2122',
                0x9A => '\u0161', 0x9B => '\u203A', 0x9C => '\u0153', 0x9F => '\u0178',
                _ => (char)value
            };
        }

        private static void AppendReadableCharacter(StringBuilder target, char value)
        {
            switch (value)
            {
                case '\r':
                case '\v':
                    target.AppendLine();
                    break;
                case '\f':
                    target.AppendLine();
                    target.AppendLine();
                    break;
                case '\a':
                    target.Append('\t');
                    break;
                case '\t':
                    target.Append('\t');
                    break;
                case '\0':
                case '\u0001':
                case '\u0013':
                case '\u0014':
                case '\u0015':
                    break;
                default:
                    if (!char.IsControl(value))
                        target.Append(value);
                    break;
            }
        }

        private static byte[] ReadRange(CfbStream stream, long offset, int count, int maximumCount)
        {
            if (offset < 0 || count < 0 || count > maximumCount || offset > stream.Length - count)
                throw new InvalidDataException("The Word binary stream contains an out-of-range reference.");

            byte[] buffer = new byte[count];
            stream.Position = offset;
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, total, count - total);
                if (read == 0)
                    throw new EndOfStreamException();
                total += read;
            }
            return buffer;
        }

        private static long CheckedAdvance(long offset, long count, long streamLength)
        {
            long result = checked(offset + count);
            if (result < 0 || result > streamLength)
                throw new InvalidDataException("The Word FIB is truncated.");
            return result;
        }

        private static ushort ReadUInt16(byte[] source, int offset)
        {
            EnsureRange(source, offset, 2);
            return BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset, 2));
        }

        private static uint ReadUInt32(byte[] source, int offset)
        {
            EnsureRange(source, offset, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset, 4));
        }

        private static int ReadInt32(byte[] source, int offset)
        {
            EnsureRange(source, offset, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, 4));
        }

        private static void EnsureRange(byte[] source, int offset, int count)
        {
            if (source == null || offset < 0 || count < 0 || offset > source.Length - count)
                throw new InvalidDataException("The Word binary structure is truncated.");
        }

        private readonly struct FibTextInfo
        {
            public FibTextInfo(bool useOneTable, bool isEncrypted, int mainTextCharacters, uint clxOffset, int clxLength)
            {
                UseOneTable = useOneTable;
                IsEncrypted = isEncrypted;
                MainTextCharacters = mainTextCharacters;
                ClxOffset = clxOffset;
                ClxLength = clxLength;
            }

            public bool UseOneTable { get; }
            public bool IsEncrypted { get; }
            public int MainTextCharacters { get; }
            public uint ClxOffset { get; }
            public int ClxLength { get; }
        }
    }
}
