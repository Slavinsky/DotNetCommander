using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class ArchiveOperationResult
    {
        public FileOperationRunResult RunResult { get; set; }
        public int CompletedEntries { get; set; }
        public int SkippedEntries { get; set; }
    }

    internal static class ArchiveService
    {
        private const int BufferSize = 1024 * 1024;

        public static Task<ArchiveOperationResult> CreateArchiveAsync(
            string[] sources,
            string destinationPath,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => CreateArchive(sources, destinationPath, progress, cancellationToken),
                cancellationToken);
        }

        public static Task<ArchiveOperationResult> ExtractArchiveAsync(
            string archivePath,
            string destinationDirectory,
            bool overwriteExisting,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => ExtractArchive(archivePath, destinationDirectory, overwriteExisting, null, null, progress, cancellationToken),
                cancellationToken);
        }

        public static Task<ArchiveOperationResult> ExtractArchiveEntriesAsync(
            string archivePath,
            string destinationDirectory,
            bool overwriteExisting,
            IReadOnlyCollection<string> selectedEntries,
            string entryRoot,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => ExtractArchive(archivePath, destinationDirectory, overwriteExisting, selectedEntries, entryRoot, progress, cancellationToken),
                cancellationToken);
        }

        private static ArchiveOperationResult CreateArchive(
            string[] sources,
            string destinationPath,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (!FileTypeClassifier.TryGetArchiveFormatByExtension(destinationPath, out ArchiveFormat format))
            {
                throw new NotSupportedException(Language.getString("archiveUnsupportedFormat"));
            }

            progress?.Report(new FileOperationProgressInfo { Phase = FileOperationPhase.Preparing });
            ArchivePlan plan = BuildCreatePlan(sources, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new DirectoryNotFoundException(destinationPath);
            }

            Directory.CreateDirectory(destinationDirectory);
            string temporaryPath = CreateTemporaryPath(destinationPath);
            var state = new ArchiveProgressState(plan.Entries.Count, plan.TotalBytes, progress);

            try
            {
                if (format == ArchiveFormat.Zip)
                {
                    CreateZip(plan, temporaryPath, state, cancellationToken);
                }
                else
                {
                    CreateTar(plan, temporaryPath, format == ArchiveFormat.TarGZip, state, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, destinationPath, true);
                return new ArchiveOperationResult
                {
                    RunResult = FileOperationRunResult.Completed,
                    CompletedEntries = state.CompletedEntries
                };
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(temporaryPath);
                return new ArchiveOperationResult
                {
                    RunResult = FileOperationRunResult.Cancelled,
                    CompletedEntries = state.CompletedEntries
                };
            }
            catch
            {
                TryDeleteFile(temporaryPath);
                throw;
            }
        }

        private static ArchiveOperationResult ExtractArchive(
            string archivePath,
            string destinationDirectory,
            bool overwriteExisting,
            IReadOnlyCollection<string> selectedEntries,
            string entryRoot,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (!FileTypeClassifier.TryDetectArchiveFormat(archivePath, out ArchiveFormat format))
            {
                throw new NotSupportedException(Language.getString("archiveUnsupportedFormat"));
            }

            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException(archivePath, archivePath);
            }

            string destinationRoot = Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(destinationRoot);
            progress?.Report(new FileOperationProgressInfo { Phase = FileOperationPhase.Preparing });

            var state = new ArchiveProgressState(0, 0, progress);
            try
            {
                if (format == ArchiveFormat.Zip)
                {
                    ExtractZip(archivePath, destinationRoot, overwriteExisting, selectedEntries, entryRoot, state, cancellationToken);
                }
                else
                {
                    ExtractTar(archivePath, destinationRoot, format == ArchiveFormat.TarGZip, overwriteExisting, selectedEntries, entryRoot, state, cancellationToken);
                }

                return new ArchiveOperationResult
                {
                    RunResult = FileOperationRunResult.Completed,
                    CompletedEntries = state.CompletedEntries,
                    SkippedEntries = state.SkippedEntries
                };
            }
            catch (OperationCanceledException)
            {
                return new ArchiveOperationResult
                {
                    RunResult = FileOperationRunResult.Cancelled,
                    CompletedEntries = state.CompletedEntries,
                    SkippedEntries = state.SkippedEntries
                };
            }
        }

        private static ArchivePlan BuildCreatePlan(string[] sources, CancellationToken cancellationToken)
        {
            var plan = new ArchivePlan();
            foreach (string source in (sources ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(source);
                string entryName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (File.Exists(fullPath))
                {
                    var file = new FileInfo(fullPath);
                    plan.Add(new ArchivePlanEntry(fullPath, NormalizeEntryName(entryName), false, file.Length, file.LastWriteTimeUtc));
                }
                else if (Directory.Exists(fullPath))
                {
                    AppendDirectory(plan, new DirectoryInfo(fullPath), NormalizeEntryName(entryName), cancellationToken);
                }
            }

            if (plan.Entries.Count == 0)
            {
                throw new InvalidOperationException(Language.getString("archiveNoItems"));
            }

            return plan;
        }

        private static void AppendDirectory(
            ArchivePlan plan,
            DirectoryInfo directory,
            string entryName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            plan.Add(new ArchivePlanEntry(directory.FullName, EnsureDirectoryEntryName(entryName), true, 0, directory.LastWriteTimeUtc));

            foreach (FileSystemInfo child in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                string childEntryName = NormalizeEntryName(entryName + "/" + child.Name);
                if (child is DirectoryInfo childDirectory)
                {
                    AppendDirectory(plan, childDirectory, childEntryName, cancellationToken);
                }
                else if (child is FileInfo childFile)
                {
                    plan.Add(new ArchivePlanEntry(
                        childFile.FullName,
                        childEntryName,
                        false,
                        childFile.Length,
                        childFile.LastWriteTimeUtc));
                }
            }
        }

        private static void CreateZip(
            ArchivePlan plan,
            string temporaryPath,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            using FileStream output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, false);

            foreach (ArchivePlanEntry item in plan.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entryName = item.IsDirectory ? EnsureDirectoryEntryName(item.EntryName) : item.EntryName;
                ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = ClampZipTimestamp(item.LastWriteTimeUtc);

                if (!item.IsDirectory)
                {
                    using Stream input = OpenRead(item.SourcePath);
                    using Stream destination = entry.Open();
                    CopyStream(input, destination, item.EntryName, state, cancellationToken);
                }

                state.CompleteEntry(item.EntryName);
            }
        }

        private static void CreateTar(
            ArchivePlan plan,
            string temporaryPath,
            bool gzip,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            using FileStream fileOutput = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan);
            using Stream archiveOutput = gzip
                ? new GZipStream(fileOutput, CompressionLevel.Optimal, false)
                : fileOutput;
            using var writer = new TarWriter(archiveOutput, TarEntryFormat.Pax, true);

            foreach (ArchivePlanEntry item in plan.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.IsDirectory)
                {
                    var directoryEntry = new PaxTarEntry(TarEntryType.Directory, EnsureDirectoryEntryName(item.EntryName))
                    {
                        ModificationTime = new DateTimeOffset(item.LastWriteTimeUtc, TimeSpan.Zero)
                    };
                    writer.WriteEntry(directoryEntry);
                }
                else
                {
                    using Stream source = OpenRead(item.SourcePath);
                    using var progressStream = new ProgressReadStream(
                        source,
                        bytes => state.AddBytes(item.EntryName, bytes),
                        cancellationToken);
                    var fileEntry = new PaxTarEntry(TarEntryType.RegularFile, item.EntryName)
                    {
                        DataStream = progressStream,
                        ModificationTime = new DateTimeOffset(item.LastWriteTimeUtc, TimeSpan.Zero)
                    };
                    writer.WriteEntry(fileEntry);
                }

                state.CompleteEntry(item.EntryName);
            }
        }

        private static void ExtractZip(
            string archivePath,
            string destinationRoot,
            bool overwriteExisting,
            IReadOnlyCollection<string> selectedEntries,
            string entryRoot,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            using FileStream input = OpenRead(archivePath);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
            ZipArchiveEntry[] entries = archive.Entries
                .Where(entry => ShouldExtractEntry(entry.FullName, selectedEntries))
                .ToArray();
            state.SetTotals(entries.Length, entries.Sum(entry => entry.Length));

            foreach (ZipArchiveEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string extractionName = GetExtractionEntryName(entry.FullName, entryRoot);
                string targetPath = ResolveSafeExtractionPath(destinationRoot, extractionName);
                bool isDirectory = string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/", StringComparison.Ordinal);
                if (isDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                    state.CompleteEntry(entry.FullName);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                if (File.Exists(targetPath) && !overwriteExisting)
                {
                    state.SkipEntry(entry.FullName);
                    continue;
                }

                using Stream source = entry.Open();
                ExtractFile(source, targetPath, overwriteExisting, entry.FullName, state, cancellationToken);
                TrySetLastWriteTime(targetPath, entry.LastWriteTime.UtcDateTime);
                state.CompleteEntry(entry.FullName);
            }
        }

        private static void ExtractTar(
            string archivePath,
            string destinationRoot,
            bool gzip,
            bool overwriteExisting,
            IReadOnlyCollection<string> selectedEntries,
            string entryRoot,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            using FileStream fileInput = OpenRead(archivePath);
            using Stream archiveInput = gzip ? new GZipStream(fileInput, CompressionMode.Decompress, false) : fileInput;
            using var reader = new TarReader(archiveInput, true);

            TarEntry entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ShouldExtractEntry(entry.Name, selectedEntries))
                {
                    continue;
                }

                string extractionName = GetExtractionEntryName(entry.Name, entryRoot);
                string targetPath = ResolveSafeExtractionPath(destinationRoot, extractionName);
                if (entry.EntryType == TarEntryType.Directory)
                {
                    Directory.CreateDirectory(targetPath);
                    state.CompleteEntry(entry.Name);
                    continue;
                }

                if (entry.DataStream == null)
                {
                    state.SkipEntry(entry.Name);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                if (File.Exists(targetPath) && !overwriteExisting)
                {
                    state.SkipEntry(entry.Name);
                    continue;
                }

                ExtractFile(entry.DataStream, targetPath, overwriteExisting, entry.Name, state, cancellationToken);
                TrySetLastWriteTime(targetPath, entry.ModificationTime.UtcDateTime);
                state.CompleteEntry(entry.Name);
            }
        }

        private static void ExtractFile(
            Stream source,
            string targetPath,
            bool overwriteExisting,
            string displayName,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            string temporaryPath = CreateTemporaryPath(targetPath);
            try
            {
                using (FileStream destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan))
                {
                    CopyStream(source, destination, displayName, state, cancellationToken);
                }

                File.Move(temporaryPath, targetPath, overwriteExisting);
            }
            catch
            {
                TryDeleteFile(temporaryPath);
                throw;
            }
        }

        private static void CopyStream(
            Stream source,
            Stream destination,
            string currentItem,
            ArchiveProgressState state,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[BufferSize];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, read);
                state.AddBytes(currentItem, read);
            }
        }

        private static string ResolveSafeExtractionPath(string destinationRoot, string entryName)
        {
            string normalizedName = (entryName ?? string.Empty)
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidDataException(Language.getString("archiveUnsafeEntry"));
            }

            string[] pathSegments = normalizedName.Split(
                new[] { Path.DirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Any(segment =>
                    segment == "." ||
                    segment == ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                throw new InvalidDataException(Language.getString("archiveUnsafeEntry") + Environment.NewLine + entryName);
            }

            string targetPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedName));
            string rootWithSeparator = destinationRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!targetPath.Equals(destinationRoot, StringComparison.OrdinalIgnoreCase) &&
                !targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(Language.getString("archiveUnsafeEntry") + Environment.NewLine + entryName);
            }

            return targetPath;
        }

        private static bool ShouldExtractEntry(string entryName, IReadOnlyCollection<string> selectedEntries)
        {
            if (selectedEntries == null || selectedEntries.Count == 0)
            {
                return true;
            }

            string normalizedEntry = NormalizeEntryName(entryName).TrimEnd('/');
            foreach (string selectedEntry in selectedEntries)
            {
                string normalizedSelection = NormalizeEntryName(selectedEntry).TrimEnd('/');
                if (string.Equals(normalizedEntry, normalizedSelection, StringComparison.OrdinalIgnoreCase) ||
                    normalizedEntry.StartsWith(normalizedSelection + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetExtractionEntryName(string entryName, string entryRoot)
        {
            string normalizedEntry = NormalizeEntryName(entryName).TrimStart('/');
            string normalizedRoot = NormalizeEntryName(entryRoot).Trim('/');
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return normalizedEntry;
            }

            string prefix = normalizedRoot + "/";
            return normalizedEntry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? normalizedEntry.Substring(prefix.Length)
                : normalizedEntry;
        }

        private static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
        }

        private static string NormalizeEntryName(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string EnsureDirectoryEntryName(string value)
        {
            string normalized = NormalizeEntryName(value).TrimEnd('/');
            return normalized + "/";
        }

        private static DateTimeOffset ClampZipTimestamp(DateTime utcValue)
        {
            DateTime minimum = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime maximum = new DateTime(2107, 12, 31, 23, 59, 58, DateTimeKind.Utc);
            DateTime clamped = utcValue < minimum ? minimum : utcValue > maximum ? maximum : utcValue;
            return new DateTimeOffset(clamped, TimeSpan.Zero);
        }

        private static string CreateTemporaryPath(string targetPath)
        {
            return targetPath + ".partial-" + Guid.NewGuid().ToString("N");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void TrySetLastWriteTime(string path, DateTime utcValue)
        {
            try
            {
                File.SetLastWriteTimeUtc(path, utcValue);
            }
            catch
            {
            }
        }

        private sealed class ArchivePlan
        {
            public List<ArchivePlanEntry> Entries { get; } = new List<ArchivePlanEntry>();
            public long TotalBytes { get; private set; }

            public void Add(ArchivePlanEntry entry)
            {
                Entries.Add(entry);
                TotalBytes += entry.Length;
            }
        }

        private sealed class ArchivePlanEntry
        {
            public ArchivePlanEntry(string sourcePath, string entryName, bool isDirectory, long length, DateTime lastWriteTimeUtc)
            {
                SourcePath = sourcePath;
                EntryName = entryName;
                IsDirectory = isDirectory;
                Length = length;
                LastWriteTimeUtc = lastWriteTimeUtc;
            }

            public string SourcePath { get; }
            public string EntryName { get; }
            public bool IsDirectory { get; }
            public long Length { get; }
            public DateTime LastWriteTimeUtc { get; }
        }

        private sealed class ArchiveProgressState
        {
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private readonly IProgress<FileOperationProgressInfo> progress;

            public ArchiveProgressState(int totalEntries, long totalBytes, IProgress<FileOperationProgressInfo> progress)
            {
                TotalEntries = totalEntries;
                TotalBytes = totalBytes;
                this.progress = progress;
            }

            public int CompletedEntries { get; private set; }
            public int SkippedEntries { get; private set; }
            public int TotalEntries { get; private set; }
            public long CompletedBytes { get; private set; }
            public long TotalBytes { get; private set; }

            public void SetTotals(int totalEntries, long totalBytes)
            {
                TotalEntries = totalEntries;
                TotalBytes = totalBytes;
            }

            public void AddBytes(string currentItem, int count)
            {
                CompletedBytes += count;
                Report(currentItem);
            }

            public void CompleteEntry(string currentItem)
            {
                CompletedEntries++;
                Report(currentItem);
            }

            public void SkipEntry(string currentItem)
            {
                SkippedEntries++;
                CompletedEntries++;
                Report(currentItem);
            }

            private void Report(string currentItem)
            {
                progress?.Report(new FileOperationProgressInfo
                {
                    Phase = FileOperationPhase.Running,
                    CurrentItem = currentItem,
                    CompletedEntries = CompletedEntries,
                    TotalEntries = TotalEntries,
                    CompletedBytes = CompletedBytes,
                    TotalBytes = TotalBytes,
                    Elapsed = stopwatch.Elapsed
                });
            }
        }

        private sealed class ProgressReadStream : Stream
        {
            private readonly Stream inner;
            private readonly Action<int> onRead;
            private readonly CancellationToken cancellationToken;

            public ProgressReadStream(Stream inner, Action<int> onRead, CancellationToken cancellationToken)
            {
                this.inner = inner;
                this.onRead = onRead;
                this.cancellationToken = cancellationToken;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }
            public override void Flush() => inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = inner.Read(buffer, offset, count);
                if (read > 0)
                {
                    onRead?.Invoke(read);
                }

                return read;
            }
        }
    }
}
