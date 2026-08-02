using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetCommander
{
    internal static class TextFileEncodingService
    {
        private const int DetectionPrefixLength = 1024;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, false);
        private static readonly Regex XmlEncodingPattern = new Regex(
            @"\bencoding\s*=\s*[""'](?<name>[A-Za-z][A-Za-z0-9._-]*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static TextFileEncodingService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string ReadAllText(string path, out Encoding encoding)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.SequentialScan);

            int prefixLength = (int)Math.Min(stream.Length, DetectionPrefixLength);
            byte[] prefix = new byte[prefixLength];
            int length = ReadPrefix(stream, prefix);
            encoding = DetectEncoding(prefix, length);
            stream.Position = 0;

            using var reader = new StreamReader(
                stream,
                encoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);
            return reader.ReadToEnd();
        }

        public static void WriteAllText(string path, string content, Encoding encoding)
        {
            File.WriteAllText(path, content, encoding ?? Utf8WithoutBom);
        }

        internal static Encoding DetectEncoding(byte[] prefix, int length)
        {
            if (prefix == null || length <= 0)
                return Utf8WithoutBom;

            length = Math.Min(length, prefix.Length);
            if (length >= 4)
            {
                if (prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0xFE && prefix[3] == 0xFF)
                    return new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: false);
                if (prefix[0] == 0xFF && prefix[1] == 0xFE && prefix[2] == 0x00 && prefix[3] == 0x00)
                    return new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: false);
            }

            if (length >= 3 &&
                prefix[0] == 0xEF &&
                prefix[1] == 0xBB &&
                prefix[2] == 0xBF)
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false);
            }

            if (length >= 2)
            {
                if (prefix[0] == 0xFE && prefix[1] == 0xFF)
                    return new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: false);
                if (prefix[0] == 0xFF && prefix[1] == 0xFE)
                    return new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: false);
            }

            if (length >= 4)
            {
                if (prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0x00 && prefix[3] == (byte)'<')
                    return new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: false);
                if (prefix[0] == (byte)'<' && prefix[1] == 0x00 && prefix[2] == 0x00 && prefix[3] == 0x00)
                    return new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: false);
                if (prefix[0] == 0x00 && prefix[1] == (byte)'<' && prefix[2] == 0x00 && prefix[3] == (byte)'?')
                    return new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: false);
                if (prefix[0] == (byte)'<' && prefix[1] == 0x00 && prefix[2] == (byte)'?' && prefix[3] == 0x00)
                    return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: false);
            }

            string header = Encoding.ASCII.GetString(prefix, 0, length);
            int offset = 0;
            while (offset < header.Length && char.IsWhiteSpace(header[offset]))
                offset++;
            if (!header.AsSpan(offset).StartsWith("<?xml".AsSpan(), StringComparison.Ordinal))
                return Utf8WithoutBom;

            int declarationEnd = header.IndexOf("?>", offset, StringComparison.Ordinal);
            string declaration = declarationEnd >= 0
                ? header.Substring(offset, declarationEnd - offset + 2)
                : header.Substring(offset);
            Match match = XmlEncodingPattern.Match(declaration);
            if (!match.Success)
                return Utf8WithoutBom;

            try
            {
                string declaredName = match.Groups["name"].Value;
                if (string.Equals(declaredName, "utf-8", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(declaredName, "utf8", StringComparison.OrdinalIgnoreCase))
                {
                    return Utf8WithoutBom;
                }

                return Encoding.GetEncoding(
                    declaredName,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ReplacementFallback);
            }
            catch (ArgumentException)
            {
                return Utf8WithoutBom;
            }
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
    }
}
