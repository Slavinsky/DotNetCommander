using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal enum FileOperationPhase
    {
        Preparing,
        Running,
        Cancelling
    }

    internal enum FileOperationRunResult
    {
        Completed,
        CompletedWithErrors,
        Cancelled
    }

    internal enum FileOperationFailureAction
    {
        Retry,
        Skip,
        Cancel
    }

    internal sealed class FileOperationFailure
    {
        public FileOperationFailure(string sourcePath, string destinationPath, Exception exception, int attempt)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            Exception = exception;
            Attempt = attempt;
        }

        public string SourcePath { get; }
        public string DestinationPath { get; }
        public Exception Exception { get; }
        public int Attempt { get; }
    }

    internal sealed class FileOperationResult
    {
        public FileOperationRunResult RunResult { get; set; }
        public int CompletedEntries { get; set; }
        public int SkippedEntries { get; set; }
        public int FailedEntries { get; set; }
        public int TotalEntries { get; set; }
        public long CompletedBytes { get; set; }
        public TimeSpan Elapsed { get; set; }
        public List<FileOperationFailure> Failures { get; } = new List<FileOperationFailure>();
    }

    internal sealed class FileOperationProgressInfo
    {
        public FileOperationPhase Phase { get; set; }
        public string CurrentItem { get; set; }
        public int CompletedEntries { get; set; }
        public int TotalEntries { get; set; }
        public long CompletedBytes { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    internal static class FileOperationService
    {
        private const int BufferSize = 1024 * 1024;

        public static Task<FileOperationResult> ExecuteCopyOrMoveAsync(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            IProgress<FileOperationProgressInfo> progress,
            Func<FileOperationFailure, FileOperationFailureAction> failureHandler,
            CancellationToken cancellationToken)
        {
            return FileOperationQueue.Shared.EnqueueAsync(
                () => ExecuteCopyOrMoveInternal(sources, destination, operationType, overwriteExistingFiles, conflictResolutions, progress, failureHandler, cancellationToken),
                cancellationToken);
        }

        public static Task<FileOperationResult> ExecuteDeleteAsync(
            string[] sources,
            IProgress<FileOperationProgressInfo> progress,
            Func<FileOperationFailure, FileOperationFailureAction> failureHandler,
            CancellationToken cancellationToken)
        {
            return FileOperationQueue.Shared.EnqueueAsync(
                () => ExecuteDeleteInternal(sources, progress, failureHandler, cancellationToken),
                cancellationToken);
        }

        private static FileOperationResult ExecuteCopyOrMoveInternal(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            IProgress<FileOperationProgressInfo> progress,
            Func<FileOperationFailure, FileOperationFailureAction> failureHandler,
            CancellationToken cancellationToken)
        {
            sources ??= Array.Empty<string>();
            progress?.Report(new FileOperationProgressInfo
            {
                Phase = FileOperationPhase.Preparing,
                TotalEntries = 0,
                TotalBytes = 0
            });

            FileOperationPlan plan;
            try
            {
                plan = BuildCopyOrMovePlan(sources, destination, operationType, overwriteExistingFiles, conflictResolutions, cancellationToken);
            }
            catch (Exception ex) when (ex is DirectoryAccessDeniedException || ex is UnauthorizedAccessException)
            {
                return CreateAccessDeniedResult(sources, ex);
            }

            return ExecutePlan(plan, progress, failureHandler, cancellationToken);
        }

        public static IReadOnlyList<FileOperationConflict> CollectCopyOrMoveConflicts(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            CancellationToken cancellationToken)
        {
            List<FileOperationConflict> conflicts = new List<FileOperationConflict>();
            HashSet<string> seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            sources ??= Array.Empty<string>();
            bool singleSource = sources.Length == 1;

            foreach (string source in sources.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string target = ResolveTargetPath(source, destination, singleSource);
                    if (FileSystemService.DirectoryExists(source))
                    {
                        CollectDirectoryConflicts(conflicts, seenTargets, source, target, operationType, cancellationToken);
                    }
                    else if (FileSystemService.FileExists(source))
                    {
                        CollectFileConflict(conflicts, seenTargets, source, target);
                    }
                }
                catch (DirectoryAccessDeniedException ex)
                {
                    LogService.LogException("FileOperationService.CollectCopyOrMoveConflicts", ex);
                    return conflicts;
                }
            }

            return conflicts;
        }

        private static FileOperationResult ExecuteDeleteInternal(
            string[] sources,
            IProgress<FileOperationProgressInfo> progress,
            Func<FileOperationFailure, FileOperationFailureAction> failureHandler,
            CancellationToken cancellationToken)
        {
            sources ??= Array.Empty<string>();
            progress?.Report(new FileOperationProgressInfo
            {
                Phase = FileOperationPhase.Preparing,
                TotalEntries = 0,
                TotalBytes = 0
            });

            FileOperationPlan plan;
            try
            {
                plan = BuildDeletePlan(sources, cancellationToken);
            }
            catch (Exception ex) when (ex is DirectoryAccessDeniedException || ex is UnauthorizedAccessException)
            {
                return CreateAccessDeniedResult(sources, ex);
            }

            return ExecutePlan(plan, progress, failureHandler, cancellationToken);
        }

        private static FileOperationResult CreateAccessDeniedResult(string[] sources, Exception exception)
        {
            LogService.LogException("FileOperationService.BuildPlan", exception);

            string sourcePath = exception is DirectoryAccessDeniedException denied
                ? denied.Path
                : sources != null && sources.Length > 0 ? sources[0] : null;

            FileOperationResult result = new FileOperationResult
            {
                RunResult = FileOperationRunResult.CompletedWithErrors,
                TotalEntries = 1,
                FailedEntries = 1
            };
            result.Failures.Add(new FileOperationFailure(sourcePath, null, exception, 1));
            return result;
        }

        private static FileOperationPlan BuildCopyOrMovePlan(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            CancellationToken cancellationToken)
        {
            FileOperationPlan plan = new FileOperationPlan();
            bool singleSource = sources.Length == 1;

            foreach (string source in sources.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (cancellationToken.IsCancellationRequested)
                    return plan;

                string target = ResolveTargetPath(source, destination, singleSource);
                if (FileSystemService.DirectoryExists(source))
                {
                    AppendDirectoryEntries(plan, source, target, operationType, overwriteExistingFiles, conflictResolutions, cancellationToken);
                }
                else if (FileSystemService.FileExists(source))
                {
                    AppendFileEntry(plan, source, target, operationType, overwriteExistingFiles, conflictResolutions, cancellationToken);
                }
            }

            return plan;
        }

        private static FileOperationPlan BuildDeletePlan(string[] sources, CancellationToken cancellationToken)
        {
            FileOperationPlan plan = new FileOperationPlan();

            foreach (string source in sources.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (cancellationToken.IsCancellationRequested)
                    return plan;

                if (FileSystemService.FileExists(source))
                {
                    long size = SafeGetFileLength(source);
                    plan.Add(new FileOperationEntry(FileOperationAction.DeleteFile, source, null, size));
                    continue;
                }

                if (!FileSystemService.DirectoryExists(source))
                    continue;

                foreach (string file in FileSystemService.CollectFiles(source))
                {
                    if (cancellationToken.IsCancellationRequested)
                        return plan;
                    plan.Add(new FileOperationEntry(FileOperationAction.DeleteFile, file, null, SafeGetFileLength(file)));
                }

                List<string> directories = FileSystemService.CollectDirectories(source)
                    .OrderByDescending(path => path.Length)
                    .ToList();

                foreach (string directory in directories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return plan;
                    plan.Add(new FileOperationEntry(FileOperationAction.DeleteDirectory, directory, null, 0));
                }

                plan.Add(new FileOperationEntry(FileOperationAction.DeleteDirectory, source, null, 0));
            }

            return plan;
        }

        private static void AppendDirectoryEntries(
            FileOperationPlan plan,
            string sourceDirectory,
            string targetDirectory,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            CancellationToken cancellationToken)
        {
            bool targetDirectoryExists = FileSystemService.DirectoryExists(targetDirectory);
            if (operationType == FormCopy.Type.Move
                && IsSameVolume(sourceDirectory, targetDirectory)
                && !targetDirectoryExists)
            {
                long directoryBytes = CalculateDirectoryBytes(sourceDirectory, cancellationToken);
                plan.Add(new FileOperationEntry(FileOperationAction.MoveDirectory, sourceDirectory, targetDirectory, directoryBytes));
                return;
            }

            plan.Add(new FileOperationEntry(FileOperationAction.CreateDirectory, sourceDirectory, targetDirectory, 0));

            List<string> directories = FileSystemService.CollectDirectories(sourceDirectory);
            foreach (string directory in directories)
            {
                if (FileSystemService.IsReparsePoint(directory))
                    continue;
                if (cancellationToken.IsCancellationRequested)
                    return;
                plan.Add(new FileOperationEntry(
                    FileOperationAction.CreateDirectory,
                    directory,
                    MapNestedTargetPath(sourceDirectory, targetDirectory, directory),
                    0));
            }

            List<string> files = FileSystemService.CollectFiles(sourceDirectory);
            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                string destinationFile = MapNestedTargetPath(sourceDirectory, targetDirectory, file);
                if (!TryResolveFileTarget(destinationFile, overwriteExistingFiles, conflictResolutions, out string resolvedDestinationFile))
                {
                    plan.MarkSkipped();
                    continue;
                }

                bool allowOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(destinationFile, conflictResolutions);
                plan.Add(new FileOperationEntry(
                    FileOperationAction.CopyFile,
                    file,
                    resolvedDestinationFile,
                    SafeGetFileLength(file),
                    allowOverwrite,
                    operationType == FormCopy.Type.Move));
            }

            if (operationType != FormCopy.Type.Move)
                return;

            foreach (string directory in directories.OrderByDescending(path => path.Length))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                plan.Add(new FileOperationEntry(
                    FileSystemService.IsReparsePoint(directory)
                        ? FileOperationAction.DeleteDirectory
                        : FileOperationAction.DeleteDirectoryIfEmpty,
                    directory,
                    null,
                    0));
            }

            plan.Add(new FileOperationEntry(
                FileSystemService.IsReparsePoint(sourceDirectory)
                    ? FileOperationAction.DeleteDirectory
                    : FileOperationAction.DeleteDirectoryIfEmpty,
                sourceDirectory,
                null,
                0));
        }

        private static void AppendFileEntry(
            FileOperationPlan plan,
            string sourceFile,
            string targetFile,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            if (!TryResolveFileTarget(targetFile, overwriteExistingFiles, conflictResolutions, out string resolvedTargetFile))
            {
                plan.MarkSkipped();
                return;
            }

            long size = SafeGetFileLength(sourceFile);
            if (operationType == FormCopy.Type.Move && IsSameVolume(sourceFile, resolvedTargetFile))
            {
                bool allowOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(targetFile, conflictResolutions);
                plan.Add(new FileOperationEntry(FileOperationAction.MoveFile, sourceFile, resolvedTargetFile, size, allowOverwrite));
                return;
            }

            bool allowCopyOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(targetFile, conflictResolutions);
            plan.Add(new FileOperationEntry(
                FileOperationAction.CopyFile,
                sourceFile,
                resolvedTargetFile,
                size,
                allowCopyOverwrite,
                operationType == FormCopy.Type.Move));
        }

        private static bool TryResolveFileTarget(
            string targetFile,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            out string resolvedTargetFile)
        {
            resolvedTargetFile = targetFile;
            if (conflictResolutions != null && conflictResolutions.TryGetValue(targetFile, out FileConflictResolution resolution))
            {
                if (resolution.Action == FileConflictResolutionAction.Skip)
                    return false;

                if (resolution.Action == FileConflictResolutionAction.Rename)
                {
                    resolvedTargetFile = resolution.DestinationPath;
                }
            }

            if (!overwriteExistingFiles &&
                (conflictResolutions == null ||
                 !conflictResolutions.TryGetValue(targetFile, out FileConflictResolution existingResolution) ||
                 existingResolution.Action != FileConflictResolutionAction.Overwrite) &&
                FileSystemService.FileExists(resolvedTargetFile))
            {
                throw new IOException("The file '" + resolvedTargetFile + "' already exists.");
            }

            return true;
        }

        private static bool ShouldOverwriteConflict(
            string targetFile,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions)
        {
            return conflictResolutions != null
                && conflictResolutions.TryGetValue(targetFile, out FileConflictResolution resolution)
                && resolution.Action == FileConflictResolutionAction.Overwrite;
        }

        private static void CollectDirectoryConflicts(
            List<FileOperationConflict> conflicts,
            HashSet<string> seenTargets,
            string sourceDirectory,
            string targetDirectory,
            FormCopy.Type operationType,
            CancellationToken cancellationToken)
        {
            bool targetDirectoryExists = FileSystemService.DirectoryExists(targetDirectory);
            if (operationType == FormCopy.Type.Move
                && IsSameVolume(sourceDirectory, targetDirectory)
                && !targetDirectoryExists)
            {
                return;
            }

            foreach (string file in FileSystemService.CollectFiles(sourceDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destinationFile = MapNestedTargetPath(sourceDirectory, targetDirectory, file);
                CollectFileConflict(conflicts, seenTargets, file, destinationFile);
            }
        }

        private static void CollectFileConflict(
            List<FileOperationConflict> conflicts,
            HashSet<string> seenTargets,
            string sourceFile,
            string targetFile)
        {
            if (!FileSystemService.FileExists(targetFile))
                return;

            if (!seenTargets.Add(targetFile))
                return;

            conflicts.Add(new FileOperationConflict(sourceFile, targetFile));
        }

        private static FileOperationResult ExecutePlan(
            FileOperationPlan plan,
            IProgress<FileOperationProgressInfo> progress,
            Func<FileOperationFailure, FileOperationFailureAction> failureHandler,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long completedBytes = 0;
            int completedEntries = 0;
            int skippedEntries = plan.SkippedEntries;
            int failedEntries = 0;
            FileOperationResult result = new FileOperationResult
            {
                TotalEntries = plan.TotalEntries + plan.SkippedEntries,
                SkippedEntries = plan.SkippedEntries
            };

            Report(progress, FileOperationPhase.Running, null, completedEntries + skippedEntries, result.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);

            foreach (FileOperationEntry entry in plan.Entries)
            {
                if (cancellationToken.IsCancellationRequested)
                    return FinishResult(result, FileOperationRunResult.Cancelled, completedEntries, skippedEntries, failedEntries, completedBytes, stopwatch.Elapsed);

                int attempt = 1;
                while (true)
                {
                    try
                    {
                        if (!ExecuteEntry(entry, progress, stopwatch, ref completedEntries, skippedEntries + failedEntries, result.TotalEntries, ref completedBytes, plan.TotalBytes, cancellationToken))
                        {
                            return FinishResult(result, FileOperationRunResult.Cancelled, completedEntries, skippedEntries, failedEntries, completedBytes, stopwatch.Elapsed);
                        }
                        break;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        FileOperationFailure failure = new FileOperationFailure(entry.SourcePath, entry.DestinationPath, ex, attempt);
                        LogService.LogException(
                            "FileOperationService." + entry.Action + " [" + (entry.SourcePath ?? entry.DestinationPath) + "]",
                            ex);
                        FileOperationFailureAction action = failureHandler?.Invoke(failure) ?? FileOperationFailureAction.Skip;
                        if (action == FileOperationFailureAction.Retry)
                        {
                            attempt++;
                            continue;
                        }

                        result.Failures.Add(failure);
                        failedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries + skippedEntries + failedEntries, result.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        if (action == FileOperationFailureAction.Cancel)
                            return FinishResult(result, FileOperationRunResult.Cancelled, completedEntries, skippedEntries, failedEntries, completedBytes, stopwatch.Elapsed);
                        break;
                    }
                }
            }

            FileOperationRunResult runResult = failedEntries > 0
                ? FileOperationRunResult.CompletedWithErrors
                : FileOperationRunResult.Completed;
            return FinishResult(result, runResult, completedEntries, skippedEntries, failedEntries, completedBytes, stopwatch.Elapsed);
        }

        private static bool ExecuteEntry(
            FileOperationEntry entry,
            IProgress<FileOperationProgressInfo> progress,
            Stopwatch stopwatch,
            ref int completedEntries,
            int progressEntryOffset,
            int totalEntries,
            ref long completedBytes,
            long totalBytes,
            CancellationToken cancellationToken)
        {
            switch (entry.Action)
            {
                case FileOperationAction.CreateDirectory:
                    FileSystemService.CreateDirectory(entry.DestinationPath);
                    break;
                case FileOperationAction.CopyFile:
                    if (!entry.CopyCommitted)
                    {
                        if (!CopyFile(entry, progress, stopwatch, ref completedEntries, progressEntryOffset, totalEntries, ref completedBytes, totalBytes, cancellationToken))
                            return false;
                    }
                    if (entry.DeleteSourceAfterCopy)
                    {
                        if (FileSystemService.FileExists(entry.SourcePath))
                            FileSystemService.DeleteFile(entry.SourcePath);
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries + progressEntryOffset, totalEntries, completedBytes, totalBytes, stopwatch.Elapsed);
                    }
                    return true;
                case FileOperationAction.MoveFile:
                    FileSystemService.EnsureParentDirectory(entry.DestinationPath);
                    FileSystemService.MoveFile(entry.SourcePath, entry.DestinationPath);
                    completedBytes += entry.SizeBytes;
                    break;
                case FileOperationAction.MoveDirectory:
                    FileSystemService.EnsureParentDirectory(entry.DestinationPath);
                    FileSystemService.MoveDirectory(entry.SourcePath, entry.DestinationPath);
                    completedBytes += entry.SizeBytes;
                    break;
                case FileOperationAction.DeleteFile:
                    if (FileSystemService.FileExists(entry.SourcePath))
                        FileSystemService.DeleteFile(entry.SourcePath);
                    completedBytes += entry.SizeBytes;
                    break;
                case FileOperationAction.DeleteDirectory:
                    if (FileSystemService.DirectoryExists(entry.SourcePath))
                        FileSystemService.DeleteDirectory(entry.SourcePath, false);
                    break;
                case FileOperationAction.DeleteDirectoryIfEmpty:
                    if (FileSystemService.DirectoryExists(entry.SourcePath)
                        && !FileSystemService.EnumerateFiles(entry.SourcePath, "*", SearchOption.TopDirectoryOnly).Any()
                        && !FileSystemService.EnumerateDirectories(entry.SourcePath, "*", SearchOption.TopDirectoryOnly).Any())
                    {
                        FileSystemService.DeleteDirectory(entry.SourcePath, false);
                    }
                    break;
            }

            completedEntries++;
            Report(progress, FileOperationPhase.Running, entry.SourcePath ?? entry.DestinationPath, completedEntries + progressEntryOffset, totalEntries, completedBytes, totalBytes, stopwatch.Elapsed);
            return true;
        }

        private static FileOperationResult FinishResult(
            FileOperationResult result,
            FileOperationRunResult runResult,
            int completedEntries,
            int skippedEntries,
            int failedEntries,
            long completedBytes,
            TimeSpan elapsed)
        {
            result.RunResult = runResult;
            result.CompletedEntries = completedEntries;
            result.SkippedEntries = skippedEntries;
            result.FailedEntries = failedEntries;
            result.CompletedBytes = completedBytes;
            result.Elapsed = elapsed;
            return result;
        }

        private static bool CopyFile(
            FileOperationEntry entry,
            IProgress<FileOperationProgressInfo> progress,
            Stopwatch stopwatch,
            ref int completedEntries,
            int progressEntryOffset,
            int totalEntries,
            ref long completedBytes,
            long totalBytes,
            CancellationToken cancellationToken)
        {
            FileSystemService.EnsureParentDirectory(entry.DestinationPath);
            string temporaryPath = entry.DestinationPath + ".dncmd-" + Guid.NewGuid().ToString("N") + ".tmp";
            long bytesWrittenThisAttempt = 0;
            try
            {
                using (FileStream sourceStream = FileSystemService.OpenRead(entry.SourcePath))
                using (FileStream destinationStream = FileSystemService.OpenWrite(temporaryPath, false))
                {
                    byte[] buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return false;
                        destinationStream.Write(buffer, 0, bytesRead);
                        bytesWrittenThisAttempt += bytesRead;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries + progressEntryOffset, totalEntries, completedBytes + bytesWrittenThisAttempt, totalBytes, stopwatch.Elapsed);
                    }

                    destinationStream.Flush(true);
                }

                FileSystemService.CommitTemporaryFile(temporaryPath, entry.DestinationPath, entry.AllowOverwrite);
                FileSystemService.CopyAttributes(entry.SourcePath, entry.DestinationPath);
                completedBytes += bytesWrittenThisAttempt;
                entry.CopyCommitted = true;
                if (!entry.DeleteSourceAfterCopy)
                {
                    completedEntries++;
                    Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries + progressEntryOffset, totalEntries, completedBytes, totalBytes, stopwatch.Elapsed);
                }
                return true;
            }
            finally
            {
                if (FileSystemService.FileExists(temporaryPath))
                {
                    try
                    {
                        FileSystemService.DeleteFile(temporaryPath);
                    }
                    catch
                    {
                        // The original failure is more useful than a temporary-file cleanup failure.
                    }
                }
            }
        }

        private static void Report(
            IProgress<FileOperationProgressInfo> progress,
            FileOperationPhase phase,
            string currentItem,
            int completedEntries,
            int totalEntries,
            long completedBytes,
            long totalBytes,
            TimeSpan elapsed)
        {
            progress?.Report(new FileOperationProgressInfo
            {
                Phase = phase,
                CurrentItem = currentItem,
                CompletedEntries = completedEntries,
                TotalEntries = totalEntries,
                CompletedBytes = completedBytes,
                TotalBytes = totalBytes,
                Elapsed = elapsed
            });
        }

        private static string ResolveTargetPath(string sourcePath, string destinationPath, bool singleSource)
        {
            if (singleSource
                && !(FileSystemService.FileExists(sourcePath) && FileSystemService.DirectoryExists(destinationPath)))
            {
                return destinationPath;
            }

            return Path.Combine(destinationPath, Path.GetFileName(sourcePath));
        }

        private static string MapNestedTargetPath(string rootSource, string rootDestination, string nestedSource)
        {
            string relativePath = nestedSource.Substring(rootSource.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(rootDestination, relativePath);
        }

        private static bool IsSameVolume(string sourcePath, string destinationPath)
        {
            string sourceRoot = FileSystemService.GetPathRoot(sourcePath);
            string destinationRoot = FileSystemService.GetPathRoot(destinationPath);
            return string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static long CalculateDirectoryBytes(string directoryPath, CancellationToken cancellationToken)
        {
            long total = 0;
            foreach (string file in FileSystemService.CollectFiles(directoryPath))
            {
                if (cancellationToken.IsCancellationRequested)
                    return total;
                total += SafeGetFileLength(file);
            }

            return total;
        }

        private static long SafeGetFileLength(string path)
        {
            return FileSystemService.GetFileLength(path);
        }

        private sealed class FileOperationPlan
        {
            public List<FileOperationEntry> Entries { get; } = new List<FileOperationEntry>();
            public int TotalEntries { get; private set; }
            public long TotalBytes { get; private set; }
            public int SkippedEntries { get; private set; }

            public void Add(FileOperationEntry entry)
            {
                Entries.Add(entry);
                TotalEntries++;
                TotalBytes += entry.SizeBytes;
            }

            public void MarkSkipped()
            {
                SkippedEntries++;
            }
        }

        private sealed class FileOperationEntry
        {
            public FileOperationEntry(FileOperationAction action, string sourcePath, string destinationPath, long sizeBytes, bool allowOverwrite = false, bool deleteSourceAfterCopy = false)
            {
                Action = action;
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
                SizeBytes = sizeBytes;
                AllowOverwrite = allowOverwrite;
                DeleteSourceAfterCopy = deleteSourceAfterCopy;
            }

            public FileOperationAction Action { get; }
            public string SourcePath { get; }
            public string DestinationPath { get; }
            public long SizeBytes { get; }
            public bool AllowOverwrite { get; }
            public bool DeleteSourceAfterCopy { get; }
            public bool CopyCommitted { get; set; }
        }

        private enum FileOperationAction
        {
            CreateDirectory,
            CopyFile,
            MoveFile,
            MoveDirectory,
            DeleteFile,
            DeleteDirectory,
            DeleteDirectoryIfEmpty
        }
    }
}
