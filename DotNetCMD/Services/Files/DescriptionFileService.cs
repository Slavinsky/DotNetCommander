using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DotNetCommander
{
    internal static class DescriptionFileService
    {
        private const string DescriptionFileName = "descript.ion";
        private const int MaxDescriptionFileBytes = 4 * 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        static DescriptionFileService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string TryGetDescription(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                    return null;

                string descriptionPath = Path.Combine(directory, DescriptionFileName);
                var info = new FileInfo(descriptionPath);
                if (!info.Exists || info.Length <= 0 || info.Length > MaxDescriptionFileBytes)
                    return null;

                byte[] bytes;
                using (var stream = new FileStream(
                    descriptionPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.SequentialScan))
                {
                    bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0)
                            break;
                        offset += read;
                    }

                    if (offset != bytes.Length)
                        Array.Resize(ref bytes, offset);
                }

                string content = Decode(bytes);
                using var reader = new StringReader(content);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!TryParseEntry(line, out string entryName, out string description))
                        continue;
                    if (!string.Equals(entryName, fileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return NormalizeDescription(description);
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("DescriptionFileService.TryGetDescription", ex);
            }

            return null;
        }

        private static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            Encoding detected = TextFileEncodingService.DetectEncoding(bytes, Math.Min(bytes.Length, 1024));
            bool hasBom = HasByteOrderMark(bytes);
            if (detected.CodePage != Encoding.UTF8.CodePage || hasBom)
                return detected.GetString(bytes).TrimStart('\uFEFF');

            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                int ansiCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                if (ansiCodePage <= 0 || ansiCodePage == Encoding.UTF8.CodePage)
                    ansiCodePage = 1252;
                return Encoding.GetEncoding(
                    ansiCodePage,
                    EncoderFallback.ReplacementFallback,
                    DecoderFallback.ReplacementFallback).GetString(bytes);
            }
        }

        private static bool HasByteOrderMark(byte[] bytes)
        {
            return (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ||
                   (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) ||
                   (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) ||
                   (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0) ||
                   (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF);
        }

        private static bool TryParseEntry(string line, out string fileName, out string description)
        {
            fileName = null;
            description = null;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            int index = 0;
            while (index < line.Length && char.IsWhiteSpace(line[index]))
                index++;
            if (index >= line.Length || line[index] == ';')
                return false;

            if (line[index] == '"')
            {
                var name = new StringBuilder();
                index++;
                bool closed = false;
                while (index < line.Length)
                {
                    if (line[index] != '"')
                    {
                        name.Append(line[index++]);
                        continue;
                    }

                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        name.Append('"');
                        index += 2;
                        continue;
                    }

                    index++;
                    closed = true;
                    break;
                }

                if (!closed || name.Length == 0)
                    return false;
                fileName = name.ToString();
            }
            else
            {
                int nameStart = index;
                while (index < line.Length && !char.IsWhiteSpace(line[index]))
                    index++;
                if (index == nameStart)
                    return false;
                fileName = line.Substring(nameStart, index - nameStart);
            }

            while (index < line.Length && char.IsWhiteSpace(line[index]))
                index++;
            if (index >= line.Length)
                return false;

            description = line.Substring(index);
            return true;
        }

        private static string NormalizeDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var result = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (char character in value.Trim().TrimStart('\u0004'))
            {
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }

                if (pendingSpace)
                    result.Append(' ');
                result.Append(character);
                pendingSpace = false;
            }

            return result.Length == 0 ? null : result.ToString();
        }
    }
}
