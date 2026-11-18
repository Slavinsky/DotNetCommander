using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetCommander
{
    /// <summary>
    /// Identifies previewable content in a materialized CFBF stream without trusting its
    /// logical filename. Unknown or ambiguous payloads stay on the binary viewer path.
    /// </summary>
    internal static class CompoundStreamPreviewClassifier
    {
        private const int MaximumProbeBytes = 16 * 1024;
        private static readonly HashSet<string> OfficeInternalStreamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WordDocument",
            "0Table",
            "1Table",
            "Book",
            "Workbook",
            "PowerPoint Document",
            "Current User",
            "EncryptedPackage",
            "EncryptionInfo",
            "\u0001Ole10Native",
            "Ole10Native",
            "Package",
            "DataSpaces",
            "StrongEncryptionDataSpace",
            "Primary",
            "CompObj",
            "ObjInfo",
            "SummaryInformation",
            "DocumentSummaryInformation",
            "\u0005SummaryInformation",
            "\u0005DocumentSummaryInformation",
            "VBA_PROJECT",
            "PROJECT",
            "PROJECTwm",
            "_VBA_PROJECT",
            "dir"
        };

        public static FileContentKind Classify(string materializedPath, string logicalStreamPath)
        {
            if (string.IsNullOrWhiteSpace(materializedPath) || !File.Exists(materializedPath))
                return FileContentKind.Binary;

            string leafName = GetLeafName(logicalStreamPath);
            if (OfficeInternalStreamNames.Contains(leafName))
            {
                return FileContentKind.Binary;
            }

            try
            {
                byte[] prefix;
                int length;
                long fileLength;
                using (var stream = new FileStream(
                    materializedPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    MaximumProbeBytes,
                    FileOptions.SequentialScan))
                {
                    fileLength = stream.Length;
                    prefix = new byte[(int)Math.Min(fileLength, MaximumProbeBytes)];
                    length = ReadPrefix(stream, prefix);
                }

                if (length == 0)
                    return FileContentKind.Binary;

                if (TryClassifyImage(prefix, length, out FileContentKind imageKind))
                    return imageKind;

                if (HasRtfSignature(prefix, length))
                    return FileContentKind.RichText;

                if (!TryDecodeText(prefix, length, length < fileLength, out string text))
                    return FileContentKind.Binary;

                bool csvHint = string.Equals(
                    Path.GetExtension(leafName),
                    ".csv",
                    StringComparison.OrdinalIgnoreCase);
                if (LooksLikeCsv(text, length == fileLength, csvHint))
                    return FileContentKind.Csv;

                return FileContentKind.Text;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                return FileContentKind.Binary;
            }
        }

        private static string GetLeafName(string logicalStreamPath)
        {
            if (string.IsNullOrWhiteSpace(logicalStreamPath))
                return string.Empty;

            int separator = logicalStreamPath.LastIndexOf('/');
            return separator >= 0 ? logicalStreamPath.Substring(separator + 1) : logicalStreamPath;
        }

        private static int ReadPrefix(Stream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }

            return totalRead;
        }

        private static bool TryClassifyImage(byte[] bytes, int length, out FileContentKind kind)
        {
            kind = FileContentKind.Binary;
            if (length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                kind = FileContentKind.Image;
                return true;
            }

            if (length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                kind = FileContentKind.Image;
                return true;
            }

            if (length >= 6 &&
                (HasAscii(bytes, 0, "GIF87a") || HasAscii(bytes, 0, "GIF89a")))
            {
                kind = FileContentKind.Image;
                return true;
            }

            if (length >= 14 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
            {
                kind = FileContentKind.Image;
                return true;
            }

            if (length >= 4 &&
                ((bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2A && bytes[3] == 0x00) ||
                 (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0x00 && bytes[3] == 0x2A) ||
                 (bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2B && bytes[3] == 0x00) ||
                 (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0x00 && bytes[3] == 0x2B)))
            {
                kind = FileContentKind.Image;
                return true;
            }

            if (length >= 12 && HasAscii(bytes, 0, "RIFF") && HasAscii(bytes, 8, "WEBP"))
            {
                kind = FileContentKind.Image;
                return true;
            }

            return false;
        }

        private static bool HasRtfSignature(byte[] bytes, int length)
        {
            int offset = 0;
            while (offset < length && offset < 16 && IsAsciiWhitespace(bytes[offset]))
                offset++;

            return length - offset >= 5 &&
                bytes[offset] == (byte)'{' &&
                bytes[offset + 1] == (byte)'\\' &&
                bytes[offset + 2] == (byte)'r' &&
                bytes[offset + 3] == (byte)'t' &&
                bytes[offset + 4] == (byte)'f';
        }

        private static bool TryDecodeText(byte[] bytes, int length, bool prefixTruncated, out string text)
        {
            text = null;
            if (length <= 0)
            {
                text = string.Empty;
                return true;
            }

            bool hasBom = HasByteOrderMark(bytes, length);
            bool hasXmlDeclaration = HasXmlDeclaration(bytes, length);
            Encoding encoding = TextFileEncodingService.DetectEncoding(bytes, length);

            try
            {
                if (!hasBom && !hasXmlDeclaration)
                    encoding = new UTF8Encoding(false, true);

                if (encoding.CodePage == Encoding.UTF8.CodePage)
                {
                    int byteOffset = length >= 3 &&
                        bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                            ? 3
                            : 0;
                    if (!TryGetValidUtf8PrefixLength(bytes, byteOffset, length, prefixTruncated, out int validLength))
                        return false;

                    text = new UTF8Encoding(false, true).GetString(bytes, byteOffset, validLength - byteOffset);
                }
                else
                {
                    int validLength = length;
                    if (prefixTruncated &&
                        (encoding.CodePage == Encoding.Unicode.CodePage || encoding.CodePage == Encoding.BigEndianUnicode.CodePage) &&
                        length >= 2)
                    {
                        int lastCodeUnit = encoding.CodePage == Encoding.Unicode.CodePage
                            ? bytes[length - 2] | (bytes[length - 1] << 8)
                            : (bytes[length - 2] << 8) | bytes[length - 1];
                        if (lastCodeUnit >= 0xD800 && lastCodeUnit <= 0xDBFF)
                            validLength -= 2;
                    }

                    text = encoding.GetString(bytes, 0, validLength);
                }

                if (text.IndexOf('\uFFFD') >= 0 || ContainsBinaryControl(text))
                {
                    text = null;
                    return false;
                }

                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
            catch (ArgumentException)
            {
                text = null;
                return false;
            }
        }

        private static bool TryGetValidUtf8PrefixLength(
            byte[] bytes,
            int start,
            int length,
            bool prefixTruncated,
            out int validLength)
        {
            validLength = length;
            int offset = start;
            while (offset < length)
            {
                byte first = bytes[offset];
                if (first <= 0x7F)
                {
                    offset++;
                    continue;
                }

                int sequenceLength;
                if (first >= 0xC2 && first <= 0xDF)
                    sequenceLength = 2;
                else if (first >= 0xE0 && first <= 0xEF)
                    sequenceLength = 3;
                else if (first >= 0xF0 && first <= 0xF4)
                    sequenceLength = 4;
                else
                    return false;

                int available = length - offset;
                int checkedContinuationCount = Math.Min(sequenceLength - 1, available - 1);
                for (int index = 1; index <= checkedContinuationCount; index++)
                {
                    byte continuation = bytes[offset + index];
                    if (continuation < 0x80 || continuation > 0xBF)
                        return false;

                    if (index == 1 &&
                        ((first == 0xE0 && continuation < 0xA0) ||
                         (first == 0xED && continuation > 0x9F) ||
                         (first == 0xF0 && continuation < 0x90) ||
                         (first == 0xF4 && continuation > 0x8F)))
                    {
                        return false;
                    }
                }

                if (available < sequenceLength)
                {
                    if (!prefixTruncated)
                        return false;

                    validLength = offset;
                    return true;
                }

                offset += sequenceLength;
            }

            return true;
        }

        private static bool HasByteOrderMark(byte[] bytes, int length)
        {
            return (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ||
                (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) ||
                (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) ||
                (length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF);
        }

        private static bool HasXmlDeclaration(byte[] bytes, int length)
        {
            Encoding encoding = TextFileEncodingService.DetectEncoding(bytes, length);
            try
            {
                string prefix = encoding.GetString(bytes, 0, length);
                int offset = prefix.Length > 0 && prefix[0] == '\uFEFF' ? 1 : 0;
                while (offset < prefix.Length && char.IsWhiteSpace(prefix[offset]))
                    offset++;

                return prefix.AsSpan(offset).StartsWith("<?xml".AsSpan(), StringComparison.Ordinal);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool ContainsBinaryControl(string value)
        {
            foreach (char character in value)
            {
                if (character == '\0' ||
                    (character < 0x20 && character != '\t' && character != '\r' && character != '\n') ||
                    (character >= 0x7F && character <= 0x9F))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeCsv(string text, bool completeSample, bool csvHint)
        {
            string[] lines = text
                .TrimStart('\uFEFF')
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Take(8)
                .ToArray();

            if (!completeSample && lines.Length > 0)
                lines = lines.Take(lines.Length - 1).ToArray();

            int minimumLines = csvHint ? 2 : 3;
            if (lines.Length < minimumLines || lines.Any(line => line.Length > 4096))
                return false;

            char[] delimiters = { ',', ';', '\t', '|' };
            int qualifyingDelimiters = 0;
            foreach (char delimiter in delimiters)
            {
                int expectedCount = -1;
                bool consistent = true;
                foreach (string line in lines)
                {
                    if (!TryCountDelimiters(line, delimiter, out int count) || count == 0)
                    {
                        consistent = false;
                        break;
                    }

                    if (expectedCount < 0)
                        expectedCount = count;
                    else if (expectedCount != count)
                    {
                        consistent = false;
                        break;
                    }
                }

                if (consistent)
                    qualifyingDelimiters++;
            }

            // More than one equally plausible delimiter makes the structure ambiguous.
            return qualifyingDelimiters == 1;
        }

        private static bool TryCountDelimiters(string line, char delimiter, out int count)
        {
            count = 0;
            bool inQuotes = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        index++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                }
                else if (character == delimiter && !inQuotes)
                {
                    count++;
                }
            }

            return !inQuotes;
        }

        private static bool HasAscii(byte[] bytes, int offset, string value)
        {
            if (offset < 0 || offset > bytes.Length - value.Length)
                return false;

            for (int index = 0; index < value.Length; index++)
            {
                if (bytes[offset + index] != (byte)value[index])
                    return false;
            }

            return true;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }
    }
}
