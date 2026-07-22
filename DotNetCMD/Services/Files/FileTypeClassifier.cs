using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DotNetCommander
{
    internal enum ArchiveFormat
    {
        Zip,
        Tar,
        TarGZip
    }

    internal enum FileComparisonMode
    {
        Binary = 0,
        Text,
        Csv,
        Image
    }

    internal static class FileTypeClassifier
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jif",".jfif",".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"
        };

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".markdown", ".log", ".cfg", ".conf", ".ini", ".json", ".jsonl", ".xml",
            ".cs", ".csproj", ".sln", ".cpp", ".h", ".hpp", ".java", ".py", ".sql", ".yaml", ".yml",".config",
            ".bat", ".cmd", ".ps1", ".psm1", ".sh", ".html", ".htm", ".css", ".js", ".ts", ".tsx",
            ".jsx", ".resx", ".props", ".targets", ".csv", ".ged"
        };

        private static readonly HashSet<string> CsvExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".csv", ".tsv", ".tab", ".psv", ".ssv"
        };

        public static bool IsSupportedArchive(string path)
        {
            return TryGetArchiveFormatByExtension(path, out _);
        }

        public static bool TryGetArchiveFormatByExtension(string path, out ArchiveFormat format)
        {
            string normalizedPath = (path ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedPath.EndsWith(".tar.gz", StringComparison.Ordinal) ||
                normalizedPath.EndsWith(".tgz", StringComparison.Ordinal))
            {
                format = ArchiveFormat.TarGZip;
                return true;
            }

            if (normalizedPath.EndsWith(".tar", StringComparison.Ordinal))
            {
                format = ArchiveFormat.Tar;
                return true;
            }

            if (normalizedPath.EndsWith(".zip", StringComparison.Ordinal))
            {
                format = ArchiveFormat.Zip;
                return true;
            }

            format = default;
            return false;
        }

        public static bool TryDetectArchiveFormat(string path, out ArchiveFormat format)
        {
            format = default;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                byte[] header = new byte[512];
                int headerLength = ReadPrefix(input, header);
                if (IsZipSignature(header, headerLength))
                {
                    format = ArchiveFormat.Zip;
                    return true;
                }

                if (headerLength >= 2 && header[0] == 0x1F && header[1] == 0x8B)
                {
                    input.Position = 0;
                    using var gzip = new GZipStream(input, CompressionMode.Decompress, true);
                    byte[] tarHeader = new byte[512];
                    int tarHeaderLength = ReadPrefix(gzip, tarHeader);
                    if (IsTarSignature(tarHeader, tarHeaderLength))
                    {
                        format = ArchiveFormat.TarGZip;
                        return true;
                    }

                    return false;
                }

                if (IsTarSignature(header, headerLength))
                {
                    format = ArchiveFormat.Tar;
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                LogService.LogException("FileTypeClassifier.TryDetectArchiveFormat", ex);
            }

            return false;
        }

        public static FileContentKind Classify(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return FileContentKind.Unknown;
            }

            if (Directory.Exists(path))
            {
                return FileContentKind.Directory;
            }

            if (IsSupportedArchive(path) || TryDetectArchiveFormat(path, out _))
            {
                return FileContentKind.Archive;
            }

            string extension = Path.GetExtension(path) ?? string.Empty;
            return ClassifyExtension(extension);
        }

        public static FileContentKind ClassifyExtension(string extension)
        {
            extension ??= string.Empty;
            if (TryGetArchiveFormatByExtension(extension, out _))
            {
                return FileContentKind.Archive;
            }

            if (CsvExtensions.Contains(extension))
            {
                return FileContentKind.Csv;
            }

            if (ImageExtensions.Contains(extension))
            {
                return FileContentKind.Image;
            }

            if (string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase))
            {
                return FileContentKind.RichText;
            }

            if (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return FileContentKind.Markdown;
            }

            if (TextExtensions.Contains(extension))
            {
                return FileContentKind.Text;
            }

            return FileContentKind.Binary;
        }

        public static FileComparisonMode GetComparisonMode(string leftPath, string rightPath)
        {
            FileContentKind leftKind = Classify(leftPath);
            FileContentKind rightKind = Classify(rightPath);

            if (leftKind == FileContentKind.Csv && rightKind == FileContentKind.Csv)
            {
                return FileComparisonMode.Csv;
            }

            if (leftKind == FileContentKind.Image && rightKind == FileContentKind.Image)
            {
                return FileComparisonMode.Image;
            }

            if (IsTextual(leftKind) && IsTextual(rightKind))
            {
                return FileComparisonMode.Text;
            }

            return FileComparisonMode.Binary;
        }

        public static bool IsTextual(FileContentKind kind)
        {
            return kind == FileContentKind.Text ||
                   kind == FileContentKind.Markdown ||
                   kind == FileContentKind.RichText;
        }

        public static bool IsMarkdown(string path)
        {
            string extension = Path.GetExtension(path) ?? string.Empty;
            return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildImageDialogFilter()
        {
            return "Image files (*.jpg;*.jpeg;*.jif;*.jfif;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff)|*.jpg;*.jpeg;*.jif;*.jfif;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff|All files (*.*)|*.*";
        }

        private static int ReadPrefix(Stream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        private static bool IsZipSignature(byte[] header, int length)
        {
            return length >= 4 &&
                header[0] == (byte)'P' &&
                header[1] == (byte)'K' &&
                ((header[2] == 3 && header[3] == 4) ||
                 (header[2] == 5 && header[3] == 6) ||
                 (header[2] == 7 && header[3] == 8));
        }

        private static bool IsTarSignature(byte[] header, int length)
        {
            return length >= 262 &&
                header[257] == (byte)'u' &&
                header[258] == (byte)'s' &&
                header[259] == (byte)'t' &&
                header[260] == (byte)'a' &&
                header[261] == (byte)'r';
        }
    }
}
