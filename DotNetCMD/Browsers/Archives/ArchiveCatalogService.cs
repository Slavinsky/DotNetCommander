using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class ArchiveCatalogEntry
    {
        public string FullName { get; set; }
        public bool IsDirectory { get; set; }
        public long Length { get; set; }
        public DateTime? Modified { get; set; }
    }

    internal static class ArchiveCatalogService
    {
        private const int BufferSize = 1024 * 1024;
        private static readonly string SessionPreviewRoot = Path.Combine(
            Path.GetTempPath(),
            "DotNetCommander",
            "ArchivePreview",
            Guid.NewGuid().ToString("N"));

        static ArchiveCatalogService()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, __) => CleanupSessionPreviewFiles();
        }

        public static Task<IReadOnlyList<ArchiveCatalogEntry>> ReadCatalogAsync(
            string archivePath,
            CancellationToken cancellationToken)
        {
            return Task.Run<IReadOnlyList<ArchiveCatalogEntry>>(
                () => ReadCatalog(archivePath, cancellationToken),
                cancellationToken);
        }

        public static Task<string> MaterializeFileAsync(
            string archivePath,
            string entryName,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => MaterializeFile(archivePath, entryName, cancellationToken),
                cancellationToken);
        }

        private static IReadOnlyList<ArchiveCatalogEntry> ReadCatalog(
            string archivePath,
            CancellationToken cancellationToken)
        {
            if (!FileTypeClassifier.TryDetectArchiveFormat(archivePath, out ArchiveFormat format))
            {
                throw new NotSupportedException(Language.getString("archiveUnsupportedFormat"));
            }

            var entries = new List<ArchiveCatalogEntry>();
            if (format == ArchiveFormat.Zip)
            {
                using FileStream input = OpenRead(archivePath);
                using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TryNormalizeEntryName(entry.FullName, out string normalizedName))
                    {
                        continue;
                    }

                    entries.Add(new ArchiveCatalogEntry
                    {
                        FullName = normalizedName,
                        IsDirectory = string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/", StringComparison.Ordinal),
                        Length = string.IsNullOrEmpty(entry.Name) ? 0 : entry.Length,
                        Modified = entry.LastWriteTime == default ? null : entry.LastWriteTime.LocalDateTime
                    });
                }
            }
            else
            {
                using FileStream fileInput = OpenRead(archivePath);
                using Stream archiveInput = format == ArchiveFormat.TarGZip
                    ? new GZipStream(fileInput, CompressionMode.Decompress, false)
                    : fileInput;
                using var reader = new TarReader(archiveInput, true);
                TarEntry entry;
                while ((entry = reader.GetNextEntry()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TryNormalizeEntryName(entry.Name, out string normalizedName))
                    {
                        continue;
                    }

                    bool isDirectory = entry.EntryType == TarEntryType.Directory;
                    if (!isDirectory && entry.DataStream == null)
                    {
                        continue;
                    }

                    entries.Add(new ArchiveCatalogEntry
                    {
                        FullName = normalizedName,
                        IsDirectory = isDirectory,
                        Length = isDirectory ? 0 : entry.Length,
                        Modified = entry.ModificationTime == default ? null : entry.ModificationTime.LocalDateTime
                    });
                }
            }

            return entries
                .GroupBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static string MaterializeFile(
            string archivePath,
            string requestedEntryName,
            CancellationToken cancellationToken)
        {
            if (!FileTypeClassifier.TryDetectArchiveFormat(archivePath, out ArchiveFormat format) ||
                !TryNormalizeEntryName(requestedEntryName, out string entryName))
            {
                throw new InvalidDataException(Language.getString("archiveUnsafeEntry"));
            }

            string previewRoot = Path.Combine(SessionPreviewRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(previewRoot);
            string outputPath = Path.Combine(previewRoot, Path.GetFileName(entryName.Replace('/', Path.DirectorySeparatorChar)));

            try
            {
                if (format == ArchiveFormat.Zip)
                {
                    using FileStream input = OpenRead(archivePath);
                    using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
                    ZipArchiveEntry entry = archive.Entries.FirstOrDefault(candidate =>
                        TryNormalizeEntryName(candidate.FullName, out string normalized) &&
                        string.Equals(normalized, entryName, StringComparison.Ordinal));
                    if (entry == null || string.IsNullOrEmpty(entry.Name))
                    {
                        throw new FileNotFoundException(entryName, entryName);
                    }

                    using Stream source = entry.Open();
                    WriteMaterializedFile(source, outputPath, cancellationToken);
                    return outputPath;
                }

                using FileStream fileInput = OpenRead(archivePath);
                using Stream archiveInput = format == ArchiveFormat.TarGZip
                    ? new GZipStream(fileInput, CompressionMode.Decompress, false)
                    : fileInput;
                using var reader = new TarReader(archiveInput, true);
                TarEntry tarEntry;
                while ((tarEntry = reader.GetNextEntry()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (tarEntry.DataStream != null &&
                        TryNormalizeEntryName(tarEntry.Name, out string normalized) &&
                        string.Equals(normalized, entryName, StringComparison.Ordinal))
                    {
                        WriteMaterializedFile(tarEntry.DataStream, outputPath, cancellationToken);
                        return outputPath;
                    }
                }

                throw new FileNotFoundException(entryName, entryName);
            }
            catch
            {
                try
                {
                    if (Directory.Exists(previewRoot))
                    {
                        Directory.Delete(previewRoot, true);
                    }
                }
                catch
                {
                }

                throw;
            }
        }

        private static void WriteMaterializedFile(Stream source, string outputPath, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[BufferSize];
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
            }
        }

        private static bool TryNormalizeEntryName(string value, out string normalizedName)
        {
            normalizedName = (value ?? string.Empty).Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            string[] segments = normalizedName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(segment =>
                    segment == "." ||
                    segment == ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                normalizedName = null;
                return false;
            }

            normalizedName = string.Join("/", segments);
            return true;
        }

        private static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
        }

        private static void CleanupSessionPreviewFiles()
        {
            try
            {
                string resolvedRoot = Path.GetFullPath(SessionPreviewRoot);
                string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DotNetCommander", "ArchivePreview"));
                if (resolvedRoot.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(resolvedRoot))
                {
                    Directory.Delete(resolvedRoot, true);
                }
            }
            catch
            {
            }
        }
    }
}
