using OpenMcdf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class CompoundRecoveryProgress
    {
        public CompoundRecoveryProgress(int entriesVisited, int streamsRecovered, long bytesRead, string currentPath)
        {
            EntriesVisited = entriesVisited;
            StreamsRecovered = streamsRecovered;
            BytesRead = bytesRead;
            CurrentPath = currentPath ?? string.Empty;
        }

        public int EntriesVisited { get; }
        public int StreamsRecovered { get; }
        public long BytesRead { get; }
        public string CurrentPath { get; }
    }

    internal sealed class CompoundRecoveryResult
    {
        public string StagedRootPath { get; set; }
        public int EntriesVisited { get; set; }
        public int StreamsRecovered { get; set; }
        public int StreamsSkipped { get; set; }
        public long BytesRead { get; set; }
        public int OmittedIssues { get; set; }
        public int OmittedRecoveredPaths { get; set; }
        public bool Incomplete { get; set; }
        public int? DestinationCopyResultCode { get; set; }
        public bool DestinationCopyStarted { get; set; }
        public string Error { get; set; }
        public List<CompoundIntegrityIssue> Issues { get; } = new List<CompoundIntegrityIssue>();
        public List<string> RecoveredPaths { get; } = new List<string>();
    }

    internal sealed class CompoundFileCatalogService : IDisposable
    {
        private const int MaximumEntries = 200000;
        private const int MaximumDepth = 256;
        internal const long MaximumExportStreamBytes = 256L * 1024 * 1024;
        internal const long MaximumExportTotalBytes = 1024L * 1024 * 1024;
        private const int MaximumRecoveryStreams = 4096;
        private const int MaximumRecoveryIssues = 100;
        private const int MaximumRecoveryManifestPaths = 1000;
        private const int MaximumRecoveryTextLength = 2048;
        private const long MaximumRecoveryFileBytes = 256L * 1024 * 1024;
        private static readonly TimeSpan MaximumRecoveryTime = TimeSpan.FromSeconds(30);
        private static readonly string MaterializationBaseRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "DotNetCommander",
            "CompoundPreview"));
        private static readonly string CleanupLockPath = Path.Combine(MaterializationBaseRoot, ".cleanup.lock");
        private readonly object lifecycleGate = new object();
        private readonly object sessionGate = new object();
        private string materializationRoot;
        private FileStream sessionLease;
        private int activeMaterializations;
        private bool disposeRequested;
        private bool cleanupStarted;

        public CompoundFileCatalogService()
        {
            // Recovery is best-effort background work. Do not make normal application or
            // panel construction depend on temp-directory availability.
            _ = Task.Run(SweepStaleMaterializationRootsSafely);
        }

        public Task<CompoundFileCatalog> ReadCatalogAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => ReadCatalog(path, cancellationToken), cancellationToken);
        }

        public async Task<string> MaterializeStreamAsync(
            string compoundPath,
            string streamPath,
            CancellationToken cancellationToken)
        {
            return await RunMaterializationAsync(
                () => MaterializeStream(compoundPath, streamPath, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> MaterializeStreamForExportAsync(
            string compoundPath,
            string streamPath,
            CancellationToken cancellationToken)
        {
            return await RunMaterializationAsync(
                () => MaterializeStreamForExport(compoundPath, streamPath, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> MaterializeEmbeddedPayloadForExportAsync(
            string compoundPath,
            string streamPath,
            CancellationToken cancellationToken)
        {
            return await RunMaterializationAsync(
                () => MaterializeEmbeddedPayloadForExport(compoundPath, streamPath, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> MaterializeStorageForExportAsync(
            string compoundPath,
            string storagePath,
            CancellationToken cancellationToken)
        {
            return await RunMaterializationAsync(
                () => MaterializeStorageForExport(compoundPath, storagePath, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<CompoundRecoveryResult> MaterializeBestEffortRecoveryAsync(
            string compoundPath,
            IProgress<CompoundRecoveryProgress> progress,
            CancellationToken cancellationToken)
        {
            lock (lifecycleGate)
            {
                if (disposeRequested)
                    throw new ObjectDisposedException(nameof(CompoundFileCatalogService));
                activeMaterializations++;
            }

            try
            {
                return await Task.Run(
                    () => MaterializeBestEffortRecovery(compoundPath, progress, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CompleteMaterialization();
            }
        }

        private async Task<string> RunMaterializationAsync(Func<string> materialize, CancellationToken cancellationToken)
        {
            lock (lifecycleGate)
            {
                if (disposeRequested)
                    throw new ObjectDisposedException(nameof(CompoundFileCatalogService));
                activeMaterializations++;
            }

            try
            {
                return await Task.Run(
                    materialize,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CompleteMaterialization();
            }
        }

        public void Dispose()
        {
            bool cleanupNow;
            lock (lifecycleGate)
            {
                if (disposeRequested)
                    return;

                disposeRequested = true;
                cleanupNow = activeMaterializations == 0;
                if (cleanupNow)
                    cleanupStarted = true;
            }

            if (cleanupNow)
                QueueCleanupSession();
        }

        public IDisposable RetainSession()
        {
            lock (lifecycleGate)
            {
                if (disposeRequested)
                    throw new ObjectDisposedException(nameof(CompoundFileCatalogService));
                activeMaterializations++;
            }

            return new SessionRetention(this);
        }

        public void ReleaseExportFiles(IEnumerable<string> paths)
        {
            if (paths == null)
                return;

            foreach (string path in paths)
            {
                try
                {
                    TryDeleteExportFile(path);
                }
                catch (Exception ex)
                {
                    LogService.LogException("CompoundFileCatalogService.ReleaseExportFile", ex);
                }
            }
        }

        private static CompoundFileCatalog ReadCatalog(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Compound file was not found.", path);

            using RootStorage root = RootStorage.OpenRead(path, StorageModeFlags.StrictValidation);
            var entries = new List<CompoundCatalogEntry>();
            var streamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnumerateStorage(root, string.Empty, 0, entries, streamNames, false, cancellationToken);
            return new CompoundFileCatalog(
                Path.GetFullPath(path),
                ClassifyDocument(streamNames, root.CLSID),
                root.CLSID,
                entries);
        }

        internal static void CleanupStaleMaterializationRoots(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
                return;

            foreach (string child in Directory.EnumerateFileSystemEntries(baseDirectory))
            {
                try
                {
                    if (Directory.Exists(child))
                        Directory.Delete(child, true);
                    else
                        File.Delete(child);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    LogService.LogException("CompoundFileCatalogService.CleanupStaleMaterializationRoots", ex);
                }
            }
        }

        private static void EnumerateStorage(
            Storage storage,
            string parentPath,
            int depth,
            List<CompoundCatalogEntry> entries,
            HashSet<string> streamNames,
            bool untrustedInherited,
            CancellationToken cancellationToken)
        {
            if (depth > MaximumDepth)
                throw new InvalidDataException("Compound storage nesting is too deep.");

            foreach (EntryInfo info in storage.EnumerateEntries())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= MaximumEntries)
                    throw new InvalidDataException("Compound file contains too many directory entries.");

                string fullPath = CombineInternalPath(parentPath, info.Name);
                bool isStorage = info.Type == EntryType.Storage;
                bool isObjectPool = string.Equals(info.Name, "ObjectPool", StringComparison.OrdinalIgnoreCase);
                entries.Add(new CompoundCatalogEntry
                {
                    Name = info.Name,
                    FullPath = fullPath,
                    ParentPath = parentPath,
                    IsStorage = isStorage,
                    Size = isStorage ? null : info.Length,
                    Created = NormalizeDate(info.CreationTime),
                    Modified = NormalizeDate(info.ModifiedTime),
                    Clsid = info.CLSID,
                    IsEmbedded = untrustedInherited || info.CLSID != Guid.Empty
                });

                if (isStorage)
                {
                    bool childUntrusted = untrustedInherited || info.CLSID != Guid.Empty || isObjectPool;
                    Storage child = storage.OpenStorage(info.Name);
                    EnumerateStorage(child, fullPath, depth + 1, entries, streamNames, childUntrusted, cancellationToken);
                }
                else
                {
                    streamNames.Add(info.Name);
                }
            }
        }

        private string MaterializeStream(string compoundPath, string streamPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(streamPath))
                return null;

            string[] segments = streamPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation);
            Storage storage = root;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                storage = storage.OpenStorage(segments[index]);
            }

            using CfbStream source = storage.OpenStream(segments[segments.Length - 1]);
            EnsureSessionRoot();
            string safeName = MakeSafeFileName(segments[segments.Length - 1]);
            string cacheKey = Path.GetFullPath(compoundPath) + "|" + File.GetLastWriteTimeUtc(compoundPath).Ticks + "|" + streamPath;
            string targetPath = Path.Combine(materializationRoot, BuildPathHash(cacheKey) + "_" + safeName);
            if (File.Exists(targetPath))
                return targetPath;
            using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            byte[] buffer = new byte[81920];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
                output.Write(buffer, 0, read);
            }

            return targetPath;
        }

        private string MaterializeStreamForExport(string compoundPath, string streamPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(streamPath))
                return null;

            string[] segments = streamPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation);
            Storage storage = root;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                storage = storage.OpenStorage(segments[index]);
            }

            using CfbStream source = storage.OpenStream(segments[segments.Length - 1]);
            long expectedLength = source.Length;
            if (expectedLength < 0 || expectedLength > MaximumExportStreamBytes)
                throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSessionRoot();

            string exportDirectory = CreateExportDirectory();
            if (IsReparsePoint(exportDirectory))
                throw new IOException("The compound export directory is a reparse point.");
            string targetPath = Path.Combine(
                exportDirectory,
                GetSafeExportFileName(segments[segments.Length - 1]));
            bool completed = false;
            bool outputCreated = false;
            try
            {
                using (var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    outputCreated = true;
                    long streamBytes = 0;
                    byte[] buffer = new byte[81920];
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = source.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        if (streamBytes > MaximumExportStreamBytes - read
                            || streamBytes > expectedLength - read)
                            throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));

                        output.Write(buffer, 0, read);
                        streamBytes += read;
                    }

                    if (streamBytes != expectedLength)
                        throw new InvalidDataException("A compound stream changed after export preflight.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                completed = true;
                return targetPath;
            }
            finally
            {
                if (!completed)
                    TryRemovePartialExport(targetPath, exportDirectory, outputCreated);
            }
        }

        private string MaterializeEmbeddedPayloadForExport(string compoundPath, string streamPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(streamPath))
                return null;

            string[] segments = streamPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            string streamName = segments[segments.Length - 1];
            bool isOle10Native = IsOle10NativeStreamName(streamName);
            bool isPackageStream = string.Equals(streamName, "Package", StringComparison.OrdinalIgnoreCase);
            if (!isOle10Native && !isPackageStream)
                throw new InvalidDataException(Language.getString("compoundPayloadNotRecognized"));

            using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation);
            Storage storage = root;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                storage = storage.OpenStorage(segments[index]);
            }

            using CfbStream source = storage.OpenStream(streamName);
            long streamLength = source.Length;
            if (streamLength < 0 || streamLength > MaximumExportStreamBytes + (isOle10Native ? 4 : 0))
                throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));

            byte[] prefix = new byte[4];
            ReadExactly(source, prefix, cancellationToken);
            long payloadLength;
            if (isOle10Native)
            {
                uint declaredLength = (uint)(prefix[0] | prefix[1] << 8 | prefix[2] << 16 | prefix[3] << 24);
                if (declaredLength > MaximumExportStreamBytes || streamLength != 4L + declaredLength)
                    throw new InvalidDataException(Language.getString("compoundPayloadNotRecognized"));
                payloadLength = declaredLength;
            }
            else
            {
                // "Package" is not a generic length-prefixed wrapper. Accept only a ZIP
                // signature and copy the complete stream without stripping any bytes.
                if (!IsZipSignature(prefix))
                    throw new InvalidDataException(Language.getString("compoundPayloadNotRecognized"));
                payloadLength = streamLength;
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureSessionRoot();
            string exportDirectory = CreateExportDirectory();
            if (IsReparsePoint(exportDirectory))
                throw new IOException("The compound export directory is a reparse point.");

            string fileName = isOle10Native ? "Ole10Native_payload.bin" : "Package_stream.bin";
            string targetPath = Path.Combine(exportDirectory, GetSafeExportFileName(fileName));
            bool completed = false;
            bool outputCreated = false;
            try
            {
                using (var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    outputCreated = true;
                    long written = 0;
                    if (isPackageStream)
                    {
                        output.Write(prefix, 0, prefix.Length);
                        written = prefix.Length;
                    }

                    byte[] buffer = new byte[81920];
                    long remaining = payloadLength - written;
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int requested = (int)Math.Min(buffer.Length, remaining);
                        int read = source.Read(buffer, 0, requested);
                        if (read <= 0)
                            throw new EndOfStreamException("The embedded payload ended before its declared length.");
                        output.Write(buffer, 0, read);
                        written += read;
                        remaining -= read;
                    }

                    if (written != payloadLength)
                        throw new InvalidDataException("The embedded payload length changed during extraction.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                completed = true;
                return targetPath;
            }
            finally
            {
                if (!completed)
                    TryRemovePartialExport(targetPath, exportDirectory, outputCreated);
            }
        }

        private static void ReadExactly(CfbStream source, byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                    throw new InvalidDataException(Language.getString("compoundPayloadNotRecognized"));
                offset += read;
            }
        }

        private static bool IsZipSignature(byte[] prefix)
        {
            return prefix.Length >= 4 && prefix[0] == 0x50 && prefix[1] == 0x4B
                && ((prefix[2] == 0x03 && prefix[3] == 0x04)
                    || (prefix[2] == 0x05 && prefix[3] == 0x06)
                    || (prefix[2] == 0x07 && prefix[3] == 0x08));
        }

        internal static bool IsOle10NativeStreamName(string name)
        {
            return string.Equals(name, "Ole10Native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "\u0001Ole10Native", StringComparison.OrdinalIgnoreCase);
        }

        private string MaterializeStorageForExport(string compoundPath, string storagePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                return null;

            string[] segments = storagePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            StorageExportPlan plan;
            using (RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation))
            {
                Storage selectedStorage = OpenStorageAtPath(root, segments);
                plan = BuildStorageExportPlan(selectedStorage, storagePath, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureSessionRoot();
            string exportDirectory = CreateExportDirectory();
            if (IsReparsePoint(exportDirectory))
                throw new IOException("The compound export directory is a reparse point.");

            string stagedStoragePath = Path.Combine(exportDirectory, GetSafeExportFileName(segments[segments.Length - 1]));
            bool stageCreated = false;
            try
            {
                if (!TryCreateDirectory(stagedStoragePath))
                    throw new IOException("The compound export storage directory already exists.");
                stageCreated = true;

                long materializedBytes = 0;
                using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation);
                foreach (StorageExportEntry entry in plan.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string destinationPath = Path.Combine(stagedStoragePath, entry.RelativePath);
                    if (entry.IsStorage)
                    {
                        if (!TryCreateDirectory(destinationPath))
                            throw new InvalidDataException(Language.getString("compoundExportNameCollision"));
                        continue;
                    }

                    string destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.Equals(destinationDirectory, stagedStoragePath, StringComparison.OrdinalIgnoreCase)
                        && !Directory.Exists(destinationDirectory))
                        throw new IOException("A parent directory for a compound stream is missing.");

                    using CfbStream source = OpenStreamAtPath(root, entry.SourcePath);
                    using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    long streamBytes = 0;
                    byte[] buffer = new byte[81920];
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = source.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        if (streamBytes > MaximumExportStreamBytes - read
                            || materializedBytes > MaximumExportTotalBytes - read
                            || streamBytes > entry.Size - read)
                            throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));

                        output.Write(buffer, 0, read);
                        streamBytes += read;
                        materializedBytes += read;
                    }

                    if (streamBytes != entry.Size)
                        throw new InvalidDataException("A compound stream changed after export preflight.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                return stagedStoragePath;
            }
            catch
            {
                if (stageCreated)
                    TryRemovePartialExportTree(stagedStoragePath, exportDirectory);
                else
                    TryRemovePartialExport(null, exportDirectory, outputCreated: false);
                throw;
            }
        }

        private CompoundRecoveryResult MaterializeBestEffortRecovery(
            string compoundPath,
            IProgress<CompoundRecoveryProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new CompoundRecoveryResult();
            string exportDirectory = null;
            string stagedRoot = null;
            var timer = Stopwatch.StartNew();
            long lastProgressBytes = 0;
            long totalBytes = 0;
            int streamsSeen = 0;
            var pending = new Stack<RecoveryFrame>();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(compoundPath) || !File.Exists(compoundPath))
                    throw new FileNotFoundException("Compound file was not found.", compoundPath);

                long fileLength = new FileInfo(compoundPath).Length;
                if (fileLength < 0 || fileLength > MaximumRecoveryFileBytes)
                {
                    result.Incomplete = true;
                    AddRecoveryIssue(result, "compoundIntegrityLimit", compoundPath,
                        Language.getString("compoundIntegrityFileLimit"));
                    return result;
                }

                exportDirectory = CreateExportDirectory();
                string safeRootName = GetSafeExportFileName(Path.GetFileNameWithoutExtension(compoundPath));
                stagedRoot = Path.Combine(exportDirectory, "Recovered-" + safeRootName);
                Directory.CreateDirectory(stagedRoot);

                using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.None);
                pending.Push(new RecoveryFrame(root, string.Empty, stagedRoot, 0));
                bool stop = false;
                while (pending.Count > 0 && !stop)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RecoveryFrame frame = pending.Pop();
                    try
                    {
                        var safeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (EntryInfo entry in frame.Storage.EnumerateEntries())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (timer.Elapsed >= MaximumRecoveryTime)
                            {
                                result.Incomplete = true;
                                AddRecoveryIssue(result, "compoundIntegrityLimit", frame.SourcePath,
                                    Language.getString("compoundIntegrityTimeLimit"));
                                stop = true;
                                break;
                            }
                            if (result.EntriesVisited >= MaximumEntries)
                            {
                                result.Incomplete = true;
                                AddRecoveryIssue(result, "compoundIntegrityLimit", frame.SourcePath,
                                    Language.getString("compoundIntegrityEntryLimit"));
                                stop = true;
                                break;
                            }

                            result.EntriesVisited++;
                            string sourcePath = string.IsNullOrEmpty(frame.SourcePath)
                                ? entry.Name
                                : frame.SourcePath + "/" + entry.Name;
                            string safeName = GetSafeExportFileName(entry.Name);
                            if (!safeNames.Add(safeName))
                            {
                                result.Incomplete = true;
                                if (entry.Type == EntryType.Stream)
                                    result.StreamsSkipped++;
                                AddRecoveryIssue(result, "compoundIntegrityLimit", sourcePath,
                                    Language.getString("compoundExportNameCollision"));
                                continue;
                            }

                            string targetPath = Path.Combine(frame.TargetDirectory, safeName);
                            if (entry.Type == EntryType.Storage)
                            {
                                if (frame.Depth >= MaximumDepth)
                                {
                                    result.Incomplete = true;
                                    AddRecoveryIssue(result, "compoundIntegrityLimit", sourcePath,
                                        Language.getString("compoundIntegrityDepthLimit"));
                                    continue;
                                }

                                try
                                {
                                    Directory.CreateDirectory(targetPath);
                                    Storage child = frame.Storage.OpenStorage(entry.Name);
                                    pending.Push(new RecoveryFrame(child, sourcePath, targetPath, frame.Depth + 1));
                                }
                                catch (Exception ex) when (IsRecoveryReadFailure(ex))
                                {
                                    result.Incomplete = true;
                                    AddRecoveryIssue(result, "compoundIntegrityStorage", sourcePath, ex.Message);
                                }
                                continue;
                            }

                            if (++streamsSeen > MaximumRecoveryStreams)
                            {
                                result.Incomplete = true;
                                AddRecoveryIssue(result, "compoundIntegrityLimit", sourcePath,
                                    Language.getString("compoundIntegrityStreamCountLimit"));
                                stop = true;
                                break;
                            }

                            long expected = entry.Length;
                            if (expected < 0 || expected > MaximumExportStreamBytes
                                || expected > MaximumExportTotalBytes - totalBytes)
                            {
                                result.Incomplete = true;
                                result.StreamsSkipped++;
                                AddRecoveryIssue(result, "compoundIntegrityLimit", sourcePath,
                                    Language.getString("compoundExportLimitExceeded"));
                                continue;
                            }

                            long streamBytes = 0;
                            bool outputCreated = false;
                            try
                            {
                                using (CfbStream source = frame.Storage.OpenStream(entry.Name))
                                {
                                    FileStream destination;
                                    try
                                    {
                                        destination = new FileStream(
                                            targetPath,
                                            FileMode.CreateNew,
                                            FileAccess.Write,
                                            FileShare.None,
                                            1024 * 1024,
                                            FileOptions.SequentialScan);
                                    }
                                    catch (Exception ex) when (IsRecoveryReadFailure(ex))
                                    {
                                        throw new CompoundRecoveryDestinationException(ex.Message);
                                    }
                                    outputCreated = true;
                                    using (destination)
                                    {
                                        byte[] buffer = new byte[1024 * 1024];
                                        while (streamBytes < expected)
                                        {
                                            cancellationToken.ThrowIfCancellationRequested();
                                            if (timer.Elapsed >= MaximumRecoveryTime)
                                            {
                                                result.Incomplete = true;
                                                throw new TimeoutException(Language.getString("compoundIntegrityTimeLimit"));
                                            }

                                            int requested = (int)Math.Min(buffer.Length, expected - streamBytes);
                                            int read = source.Read(buffer, 0, requested);
                                            if (read == 0)
                                                throw new EndOfStreamException(Language.getString("compoundIntegrityShortRead"));

                                            try
                                            {
                                                destination.Write(buffer, 0, read);
                                            }
                                            catch (Exception ex) when (IsRecoveryReadFailure(ex))
                                            {
                                                throw new CompoundRecoveryDestinationException(ex.Message);
                                            }
                                            streamBytes += read;
                                            totalBytes += read;
                                            result.BytesRead += read;
                                            if (result.BytesRead - lastProgressBytes >= 1024 * 1024)
                                            {
                                                progress?.Report(new CompoundRecoveryProgress(
                                                    result.EntriesVisited,
                                                    result.StreamsRecovered,
                                                    result.BytesRead,
                                                    sourcePath));
                                                lastProgressBytes = result.BytesRead;
                                            }
                                        }
                                        try
                                        {
                                            destination.Flush();
                                        }
                                        catch (Exception ex) when (IsRecoveryReadFailure(ex))
                                        {
                                            throw new CompoundRecoveryDestinationException(ex.Message);
                                        }
                                    }
                                }

                                result.StreamsRecovered++;
                                if (result.RecoveredPaths.Count < MaximumRecoveryManifestPaths
                                    && sourcePath.Length <= MaximumRecoveryTextLength)
                                    result.RecoveredPaths.Add(sourcePath);
                                else
                                    result.OmittedRecoveredPaths++;
                            }
                            catch (OperationCanceledException)
                            {
                                TryDeletePartialRecoveryFile(targetPath, outputCreated);
                                throw;
                            }
                            catch (CompoundRecoveryDestinationException ex)
                            {
                                result.Error = TryDeletePartialRecoveryFile(targetPath, outputCreated)
                                    ? string.Format(
                                        CultureInfo.CurrentCulture,
                                        Language.getString("compoundRecoveryWriteFailed"),
                                        ex.Message)
                                    : Language.getString("compoundRecoveryCleanupFailed");
                                stop = true;
                                break;
                            }
                            catch (Exception ex) when (IsRecoveryReadFailure(ex) || ex is TimeoutException)
                            {
                                if (!TryDeletePartialRecoveryFile(targetPath, outputCreated))
                                {
                                    result.Error = Language.getString("compoundRecoveryCleanupFailed");
                                    stop = true;
                                    break;
                                }
                                result.Incomplete = true;
                                result.StreamsSkipped++;
                                AddRecoveryIssue(result,
                                    ex is TimeoutException ? "compoundIntegrityLimit" : "compoundIntegrityStreamRead",
                                    sourcePath,
                                    ex.Message);
                                if (ex is TimeoutException)
                                {
                                    stop = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsRecoveryReadFailure(ex))
                    {
                        result.Incomplete = true;
                        AddRecoveryIssue(result, "compoundIntegrityStorage", frame.SourcePath, ex.Message);
                    }
                }

                if (result.Error != null)
                    return result;

                if (timer.Elapsed >= MaximumRecoveryTime)
                {
                    result.Incomplete = true;
                    AddRecoveryIssue(result, "compoundIntegrityLimit", string.Empty,
                        Language.getString("compoundIntegrityTimeLimit"));
                }

                if (result.StreamsRecovered == 0)
                    return result;

                WriteRecoveryManifest(stagedRoot, compoundPath, result);
                result.StagedRootPath = stagedRoot;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsRecoveryReadFailure(ex))
            {
                result.Error = ex.Message;
                AddRecoveryIssue(result, "compoundIntegrityOpen", compoundPath, ex.Message);
                return result;
            }
            finally
            {
                if (string.IsNullOrWhiteSpace(result.StagedRootPath))
                {
                    if (!string.IsNullOrEmpty(stagedRoot))
                        TryRemovePartialExportTree(stagedRoot, exportDirectory);
                    else if (!string.IsNullOrEmpty(exportDirectory))
                        TryRemovePartialExport(null, exportDirectory, outputCreated: false);
                }
            }
        }

        private static bool IsRecoveryReadFailure(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException
                || ex is System.Security.SecurityException || ex is NotSupportedException
                || ex is ArgumentException || ex is FormatException
                || ex is InvalidOperationException || ex is OverflowException;
        }

        private sealed class CompoundRecoveryDestinationException : IOException
        {
            public CompoundRecoveryDestinationException(string message) : base(message) { }
        }

        private static void AddRecoveryIssue(CompoundRecoveryResult result, string stageKey, string path, string message)
        {
            if (result.Issues.Count >= MaximumRecoveryIssues)
            {
                result.OmittedIssues++;
                return;
            }

            result.Issues.Add(new CompoundIntegrityIssue(
                stageKey,
                TruncateRecoveryText(path),
                TruncateRecoveryText(message)));
        }

        private static string TruncateRecoveryText(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaximumRecoveryTextLength)
                return value ?? string.Empty;
            return value.Substring(0, MaximumRecoveryTextLength - 1) + "…";
        }

        private static bool TryDeletePartialRecoveryFile(string path, bool outputCreated)
        {
            if (!outputCreated)
                return true;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundFileCatalogService.TryDeletePartialRecoveryFile", ex);
                return false;
            }
        }

        private static void WriteRecoveryManifest(string directory, string sourcePath, CompoundRecoveryResult result)
        {
            var manifest = new StringBuilder();
            manifest.AppendLine("DotNetCommander best-effort CFBF reachable-stream export");
            manifest.AppendLine("This is not a repair and does not verify digital signatures.");
            manifest.AppendLine("Only entries reachable through OpenMcdf's non-strict directory tree were considered.");
            manifest.AppendLine("Unlinked directory entries and streams cannot be discovered by this export.");
            manifest.AppendLine("Source: " + Path.GetFullPath(sourcePath));
            manifest.AppendLine("Complete streams exported: " + result.StreamsRecovered);
            manifest.AppendLine("Streams skipped: " + result.StreamsSkipped);
            manifest.AppendLine("Source bytes read: " + result.BytesRead);
            manifest.AppendLine("Scan incomplete: " + result.Incomplete);
            manifest.AppendLine("Recovered CFBF paths:");
            foreach (string path in result.RecoveredPaths)
                manifest.AppendLine("  " + path);
            if (result.OmittedRecoveredPaths > 0)
                manifest.AppendLine("Additional recovered paths omitted: " + result.OmittedRecoveredPaths);
            manifest.AppendLine("Skipped entries and issues:");
            foreach (CompoundIntegrityIssue issue in result.Issues)
                manifest.AppendLine(issue.StageKey + " | " + issue.Path + " | " + issue.Message);
            if (result.OmittedIssues > 0)
                manifest.AppendLine("Omitted issues: " + result.OmittedIssues);

            string name = "DotNetCommander-Recovery-Report.txt";
            string reportPath = Path.Combine(directory, name);
            for (int suffix = 2; File.Exists(reportPath) || Directory.Exists(reportPath); suffix++)
            {
                name = "DotNetCommander-Recovery-Report_" + suffix.ToString(CultureInfo.InvariantCulture) + ".txt";
                reportPath = Path.Combine(directory, name);
            }
            File.WriteAllText(reportPath, manifest.ToString(), new UTF8Encoding(false));
        }

        private readonly struct RecoveryFrame
        {
            public RecoveryFrame(Storage storage, string sourcePath, string targetDirectory, int depth)
            {
                Storage = storage;
                SourcePath = sourcePath;
                TargetDirectory = targetDirectory;
                Depth = depth;
            }

            public Storage Storage { get; }
            public string SourcePath { get; }
            public string TargetDirectory { get; }
            public int Depth { get; }
        }

        private static StorageExportPlan BuildStorageExportPlan(
            Storage selectedStorage,
            string selectedPath,
            CancellationToken cancellationToken)
        {
            var plan = new StorageExportPlan();
            int entryCount = 0;
            int selectedDepth = selectedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length;
            CollectStorageExportEntries(selectedStorage, selectedPath, string.Empty, selectedDepth, plan, ref entryCount, cancellationToken);
            return plan;
        }

        private static void CollectStorageExportEntries(
            Storage storage,
            string sourceParentPath,
            string relativeParentPath,
            int depth,
            StorageExportPlan plan,
            ref int entryCount,
            CancellationToken cancellationToken)
        {
            if (depth > MaximumDepth)
                throw new InvalidDataException("Compound storage nesting is too deep.");

            var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EntryInfo info in storage.EnumerateEntries())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entryCount > MaximumEntries)
                    throw new InvalidDataException("Compound file contains too many directory entries.");

                string safeName = GetSafeExportFileName(info.Name);
                if (!normalizedNames.Add(safeName))
                    throw new InvalidDataException(Language.getString("compoundExportNameCollision"));

                string sourcePath = CombineInternalPath(sourceParentPath, info.Name);
                string relativePath = string.IsNullOrEmpty(relativeParentPath)
                    ? safeName
                    : Path.Combine(relativeParentPath, safeName);
                if (info.Type == EntryType.Storage)
                {
                    if (depth >= MaximumDepth)
                        throw new InvalidDataException("Compound storage nesting is too deep.");

                    plan.Entries.Add(new StorageExportEntry(sourcePath, relativePath, isStorage: true, size: 0));
                    Storage child = storage.OpenStorage(info.Name);
                    CollectStorageExportEntries(child, sourcePath, relativePath, depth + 1, plan, ref entryCount, cancellationToken);
                    continue;
                }

                long size = info.Length;
                if (size < 0 || size > MaximumExportStreamBytes || plan.TotalBytes > MaximumExportTotalBytes - size)
                    throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));

                plan.TotalBytes += size;
                plan.Entries.Add(new StorageExportEntry(sourcePath, relativePath, isStorage: false, size));
            }
        }

        private static Storage OpenStorageAtPath(Storage root, string[] segments)
        {
            Storage storage = root;
            foreach (string segment in segments)
                storage = storage.OpenStorage(segment);
            return storage;
        }

        private static CfbStream OpenStreamAtPath(Storage root, string streamPath)
        {
            string[] segments = streamPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                throw new InvalidDataException("Compound stream path is empty.");

            Storage storage = OpenStorageAtPath(root, segments.Take(segments.Length - 1).ToArray());
            return storage.OpenStream(segments[segments.Length - 1]);
        }

        private sealed class StorageExportPlan
        {
            public List<StorageExportEntry> Entries { get; } = new List<StorageExportEntry>();
            public long TotalBytes { get; set; }
        }

        private sealed class StorageExportEntry
        {
            public StorageExportEntry(string sourcePath, string relativePath, bool isStorage, long size)
            {
                SourcePath = sourcePath;
                RelativePath = relativePath;
                IsStorage = isStorage;
                Size = size;
            }

            public string SourcePath { get; }
            public string RelativePath { get; }
            public bool IsStorage { get; }
            public long Size { get; }
        }

        private string CreateExportDirectory()
        {
            while (true)
            {
                string candidate = Path.Combine(materializationRoot, "Export-" + Guid.NewGuid().ToString("N"));
                if (TryCreateDirectory(candidate))
                    return candidate;
            }
        }

        private static void TryRemovePartialExport(string targetPath, string exportDirectory, bool outputCreated)
        {
            try
            {
                if (outputCreated && File.Exists(targetPath))
                    File.Delete(targetPath);
                if (Directory.Exists(exportDirectory))
                    Directory.Delete(exportDirectory, recursive: false);
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundFileCatalogService.RemovePartialExport", ex);
            }
        }

        private void TryDeleteExportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(materializationRoot) || string.IsNullOrWhiteSpace(path))
                return;

            string fullFilePath = Path.GetFullPath(path);
            string exportDirectory = Path.GetDirectoryName(fullFilePath);
            string expectedDirectory = Path.GetDirectoryName(exportDirectory);
            if (!string.Equals(expectedDirectory, materializationRoot, StringComparison.OrdinalIgnoreCase)
                || !IsExportDirectoryName(Path.GetFileName(exportDirectory))
                || IsReparsePoint(materializationRoot)
                || IsReparsePoint(exportDirectory))
                return;

            string[] entries = Directory.EnumerateFileSystemEntries(exportDirectory, "*", SearchOption.TopDirectoryOnly).ToArray();
            if (entries.Length != 1
                || !string.Equals(Path.GetFullPath(entries[0]), fullFilePath, StringComparison.OrdinalIgnoreCase))
                return;

            var files = new List<string>();
            var directories = new List<string>();
            int entryCount = 0;
            if (!TryCollectSafeExportTree(fullFilePath, files, directories, ref entryCount, 0))
                return;

            DeleteCollectedExportTree(files, directories);
            Directory.Delete(exportDirectory, recursive: false);
        }

        private void TryRemovePartialExportTree(string stagedStoragePath, string exportDirectory)
        {
            try
            {
                TryDeleteExportFile(stagedStoragePath);
                if (Directory.Exists(exportDirectory)
                    && !Directory.EnumerateFileSystemEntries(exportDirectory, "*", SearchOption.TopDirectoryOnly).Any())
                    Directory.Delete(exportDirectory, recursive: false);
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundFileCatalogService.RemovePartialExportTree", ex);
            }
        }

        private static bool TryCollectSafeExportTree(
            string path,
            List<string> files,
            List<string> directories,
            ref int entryCount,
            int depth)
        {
            if (depth > MaximumDepth || ++entryCount > MaximumEntries)
                return false;

            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0
                || !string.Equals(
                    Path.GetFileName(path),
                    GetSafeExportFileName(Path.GetFileName(path)),
                    StringComparison.Ordinal))
                return false;

            if ((attributes & FileAttributes.Directory) == 0)
            {
                files.Add(path);
                return true;
            }

            directories.Add(path);
            var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly))
            {
                string childName = Path.GetFileName(child);
                if (!normalizedNames.Add(GetSafeExportFileName(childName))
                    || !TryCollectSafeExportTree(child, files, directories, ref entryCount, depth + 1))
                    return false;
            }

            return true;
        }

        private static void DeleteCollectedExportTree(List<string> files, List<string> directories)
        {
            foreach (string file in files)
                File.Delete(file);
            foreach (string directory in directories.OrderByDescending(path => path.Length))
                Directory.Delete(directory, recursive: false);
        }

        private void CompleteMaterialization()
        {
            bool cleanupNow = false;
            lock (lifecycleGate)
            {
                activeMaterializations--;
                if (disposeRequested && activeMaterializations == 0 && !cleanupStarted)
                {
                    cleanupStarted = true;
                    cleanupNow = true;
                }
            }

            if (cleanupNow)
                QueueCleanupSession();
        }

        private void QueueCleanupSession()
        {
            _ = Task.Run(CleanupSession);
        }

        private sealed class SessionRetention : IDisposable
        {
            private CompoundFileCatalogService owner;

            public SessionRetention(CompoundFileCatalogService owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref owner, null)?.CompleteMaterialization();
            }
        }

        private void EnsureSessionRoot()
        {
            lock (sessionGate)
            {
                if (sessionLease != null)
                    return;

                Directory.CreateDirectory(MaterializationBaseRoot);
                if (IsReparsePoint(MaterializationBaseRoot))
                    throw new IOException("The compound materialization base directory is a reparse point.");

                // Coordinate root creation with the crash sweeper, then use the Windows
                // atomic directory primitive so even an unexpected GUID collision cannot
                // cause this session to adopt another process's directory.
                using FileStream cleanupLock = AcquireCleanupLock();
                while (true)
                {
                    string candidate = Path.Combine(MaterializationBaseRoot, Guid.NewGuid().ToString("N"));
                    if (!TryCreateDirectory(candidate))
                        continue;

                    if (IsReparsePoint(candidate))
                        throw new IOException("The compound materialization session directory is a reparse point.");

                    materializationRoot = candidate;
                    string sessionLockPath = Path.Combine(candidate, ".session.lock");
                    try
                    {
                        sessionLease = new FileStream(sessionLockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch
                    {
                        materializationRoot = null;
                        try
                        {
                            // Only remove the newly-created directory if it is still empty.
                            Directory.Delete(candidate, recursive: false);
                        }
                        catch (Exception cleanupException) when (cleanupException is IOException || cleanupException is UnauthorizedAccessException)
                        {
                            LogService.LogException("CompoundFileCatalogService.RemoveUnregisteredRoot", cleanupException);
                        }
                        throw;
                    }
                    return;
                }
            }
        }

        private static bool TryCreateDirectory(string path)
        {
            if (CreateDirectoryWindows(path, IntPtr.Zero))
                return true;

            int errorCode = Marshal.GetLastWin32Error();
            if (errorCode == 80 || errorCode == 183)
                return false;

            throw new Win32Exception(errorCode, "The compound materialization session directory could not be created.");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateDirectoryW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

        internal static string ClassifyDocument(ISet<string> streams, Guid rootClsid)
        {
            if (streams.Contains("WordDocument"))
                return "Microsoft Word Document";
            if (streams.Contains("Workbook") || streams.Contains("Book"))
                return "Microsoft Excel Workbook";
            if (streams.Contains("PowerPoint Document"))
                return "Microsoft PowerPoint Presentation";
            if (streams.Contains("__properties_version1.0"))
                return "Microsoft Outlook Message";
            if (streams.Contains("_Tables") && streams.Contains("_StringData"))
                return "Windows Installer Database";
            if (streams.Contains("VisioDocument"))
                return "Microsoft Visio Drawing";
            if (streams.Contains("MSProject"))
                return "Microsoft Project Document";
            if (streams.Contains("Catalog"))
                return "Windows Thumbnail Cache";

            string clsidType = ClassifyClsid(rootClsid);
            return clsidType ?? "Compound File Binary Format";
        }

        private static string ClassifyClsid(Guid clsid)
        {
            if (clsid == new Guid("00020906-0000-0000-C000-000000000046"))
                return "Microsoft Word Document";
            if (clsid == new Guid("00020820-0000-0000-C000-000000000046"))
                return "Microsoft Excel Workbook";
            if (clsid == new Guid("64818D10-4F9B-11CF-86EA-00AA00B929E8"))
                return "Microsoft PowerPoint Presentation";
            if (clsid == new Guid("00021A14-0000-0000-C000-000000000046"))
                return "Microsoft Visio Drawing";
            if (clsid == new Guid("000C1084-0000-0000-C000-000000000046"))
                return "Windows Installer Database";
            return null;
        }

        private static DateTime? NormalizeDate(DateTime value)
        {
            return value == DateTime.MinValue || value == DateTime.MaxValue ? null : value.ToLocalTime();
        }

        private static string CombineInternalPath(string parent, string name)
        {
            return string.IsNullOrEmpty(parent) ? name : parent + "/" + name;
        }

        internal static string MakeSafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(Math.Max(1, name?.Length ?? 0));
            foreach (char character in name ?? string.Empty)
            {
                builder.Append(char.IsControl(character) || invalid.Contains(character) ? '_' : character);
            }

            string result = builder.ToString().Trim().TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(result) ? "stream.bin" : result;
        }

        internal static string GetSafeExportFileName(string name)
        {
            const int maximumLength = 120;
            const string invalidCharacters = "<>:\"/\\|?*";
            var builder = new StringBuilder(Math.Max(1, name?.Length ?? 0));
            foreach (char character in name ?? string.Empty)
            {
                builder.Append(char.IsControl(character) || invalidCharacters.IndexOf(character) >= 0 ? '_' : character);
            }

            string result = builder.ToString().Trim().TrimEnd('.', ' ');
            if (result.Length > maximumLength)
            {
                result = result.Substring(0, maximumLength);
                if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1]))
                    result = result.Substring(0, result.Length - 1);
                result = result.TrimEnd('.', ' ');
            }

            if (string.IsNullOrWhiteSpace(result))
                result = "stream";

            string deviceName = result.Split('.')[0].TrimEnd(' ', '.');
            if (IsReservedWindowsDeviceName(deviceName))
                result = "_" + result;

            return result;
        }

        private static bool IsReservedWindowsDeviceName(string name)
        {
            if (string.Equals(name, "CON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "PRN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "AUX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "NUL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "CONIN$", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "CONOUT$", StringComparison.OrdinalIgnoreCase))
                return true;

            return name.Length == 4
                && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && name[3] >= '1'
                && name[3] <= '9';
        }

        private static string BuildPathHash(string path)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
            return Convert.ToHexString(hash, 0, 6);
        }

        private void CleanupSession()
        {
            if (sessionLease == null)
                return;

            FileStream cleanupLock = null;
            try
            {
                cleanupLock = AcquireCleanupLock();
                sessionLease?.Dispose();
                sessionLease = null;
                if (!TryDeleteSessionRoot(materializationRoot))
                    throw new IOException("The compound materialization session directory could not be safely removed.");
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundFileCatalogService.Dispose", ex);
            }
            finally
            {
                cleanupLock?.Dispose();
                // If cleanup could not acquire the shared lock, release the lease only after
                // every materialization has completed. A later startup sweep can reclaim it.
                sessionLease?.Dispose();
                sessionLease = null;
            }
        }

        private static FileStream AcquireCleanupLock()
        {
            Directory.CreateDirectory(MaterializationBaseRoot);
            if (IsReparsePoint(MaterializationBaseRoot))
                throw new IOException("The compound materialization base directory is a reparse point.");
            if (File.Exists(CleanupLockPath) && IsReparsePoint(CleanupLockPath))
                throw new IOException("The compound materialization cleanup lock is a reparse point.");

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                try
                {
                    return new FileStream(CleanupLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException ex) when (IsSharingViolation(ex) && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(50);
                }
            }
        }

        private static bool IsSharingViolation(IOException exception)
        {
            int errorCode = exception.HResult & 0xFFFF;
            return errorCode == 32 || errorCode == 33;
        }

        private static void SweepStaleMaterializationRoots()
        {
            if (!Directory.Exists(MaterializationBaseRoot))
                return;
            if (IsReparsePoint(MaterializationBaseRoot))
                throw new IOException("The compound materialization base directory is a reparse point.");

            foreach (string candidate in Directory.EnumerateDirectories(MaterializationBaseRoot, "*", SearchOption.TopDirectoryOnly))
            {
                if (!IsDirectGuidSessionRoot(candidate) || IsReparsePoint(candidate))
                    continue;

                string leasePath = Path.Combine(candidate, ".session.lock");
                if (!File.Exists(leasePath) || IsReparsePoint(leasePath))
                    continue;

                try
                {
                    // An active service holds this file with FileShare.None. Crashed processes
                    // release the OS handle, allowing this sweep to reclaim their root.
                    using (new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }

                    if (!TryDeleteSessionRoot(candidate))
                        continue;
                }
                catch (Exception ex)
                {
                    LogService.LogException("CompoundFileCatalogService.SweepStaleRoot", ex);
                }
            }
        }

        private static void SweepStaleMaterializationRootsSafely()
        {
            try
            {
                using FileStream cleanupLock = AcquireCleanupLock();
                SweepStaleMaterializationRoots();
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundFileCatalogService.SweepStaleRoots", ex);
            }
        }

        private static bool TryDeleteSessionRoot(string path)
        {
            if (!IsDirectGuidSessionRoot(path) || !Directory.Exists(path) || IsReparsePoint(path))
                return !Directory.Exists(path);

            string markerPath = Path.Combine(path, ".session.lock");
            if (File.Exists(markerPath) && IsReparsePoint(markerPath))
                return false;
            if (!File.Exists(markerPath))
                return false;

            string[] entries = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly).ToArray();
            var exportDirectories = new List<string>();
            var regularFiles = new List<string>();
            var exportFiles = new List<string>();
            var stagedDirectories = new List<string>();
            int exportEntryCount = 0;
            foreach (string entry in entries)
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return false;

                if ((attributes & FileAttributes.Directory) == 0)
                {
                    string name = Path.GetFileName(entry);
                    if (!string.Equals(name, ".session.lock", StringComparison.OrdinalIgnoreCase)
                        && !IsCachedMaterializationFileName(name))
                        return false;
                    regularFiles.Add(entry);
                    continue;
                }

                if (!IsExportDirectoryName(Path.GetFileName(entry)))
                    return false;

                string[] children = Directory.EnumerateFileSystemEntries(entry, "*", SearchOption.TopDirectoryOnly).Take(2).ToArray();
                if (children.Length > 1)
                    return false;
                if (children.Length == 1
                    && !TryCollectSafeExportTree(children[0], exportFiles, stagedDirectories, ref exportEntryCount, 0))
                {
                    return false;
                }
                exportDirectories.Add(entry);
            }

            DeleteCollectedExportTree(exportFiles, stagedDirectories);
            foreach (string directory in exportDirectories)
                Directory.Delete(directory, recursive: false);

            foreach (string entry in regularFiles.Where(entry => !string.Equals(entry, markerPath, StringComparison.OrdinalIgnoreCase)))
                File.Delete(entry);

            if (File.Exists(markerPath))
                File.Delete(markerPath);

            try
            {
                Directory.Delete(path, recursive: false);
                return true;
            }
            catch
            {
                // Keep a recognizable, unlocked marker if a file lock prevented final removal.
                if (Directory.Exists(path) && !File.Exists(markerPath))
                {
                    try
                    {
                        using var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        LogService.LogException("CompoundFileCatalogService.RestoreSessionMarker", ex);
                    }
                }
                throw;
            }
        }

        private static bool IsExportDirectoryName(string name)
        {
            const string prefix = "Export-";
            return name != null
                && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParseExact(name.Substring(prefix.Length), "N", out _);
        }

        private static bool IsCachedMaterializationFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 14 || name[12] != '_')
                return false;

            for (int index = 0; index < 12; index++)
            {
                char character = name[index];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'A' && character <= 'F')
                    || (character >= 'a' && character <= 'f')))
                    return false;
            }

            return string.Equals(name.Substring(13), MakeSafeFileName(name.Substring(13)), StringComparison.Ordinal);
        }

        private static bool IsDirectGuidSessionRoot(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string parent = Path.GetDirectoryName(fullPath);
                string name = Path.GetFileName(fullPath);
                return string.Equals(parent, MaterializationBaseRoot, StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParseExact(name, "N", out _);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException)
            {
                return false;
            }
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }
    }

    internal sealed class CompoundFileCatalog
    {
        public CompoundFileCatalog(string path, string documentType, Guid rootClsid, IReadOnlyList<CompoundCatalogEntry> entries)
        {
            Path = path;
            DocumentType = documentType;
            RootClsid = rootClsid;
            Entries = entries ?? Array.Empty<CompoundCatalogEntry>();
        }

        public string Path { get; }
        public string DocumentType { get; }
        public Guid RootClsid { get; }
        public IReadOnlyList<CompoundCatalogEntry> Entries { get; }
    }

    internal sealed class CompoundCatalogEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string ParentPath { get; set; }
        public bool IsStorage { get; set; }
        public bool IsEmbedded { get; set; }
        public long? Size { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }
        public Guid Clsid { get; set; }
    }
}
