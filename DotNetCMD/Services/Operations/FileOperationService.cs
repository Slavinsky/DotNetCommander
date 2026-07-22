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
        Cancelled
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

        public static Task<FileOperationRunResult> ExecuteCopyOrMoveAsync(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => ExecuteCopyOrMoveInternal(sources, destination, operationType, overwriteExistingFiles, conflictResolutions, progress, cancellationToken), cancellationToken);
        }

        public static Task<FileOperationRunResult> ExecuteDeleteAsync(
            string[] sources,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => ExecuteDeleteInternal(sources, progress, cancellationToken), cancellationToken);
        }

        private static FileOperationRunResult ExecuteCopyOrMoveInternal(
            string[] sources,
            string destination,
            FormCopy.Type operationType,
            bool overwriteExistingFiles,
            IReadOnlyDictionary<string, FileConflictResolution> conflictResolutions,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            sources ??= Array.Empty<string>();
            progress?.Report(new FileOperationProgressInfo
            {
                Phase = FileOperationPhase.Preparing,
                TotalEntries = 0,
                TotalBytes = 0
            });

            FileOperationPlan plan = BuildCopyOrMovePlan(sources, destination, operationType, overwriteExistingFiles, conflictResolutions, cancellationToken);
            return ExecutePlan(plan, overwriteExistingFiles, progress, cancellationToken);
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

            return conflicts;
        }

        private static FileOperationRunResult ExecuteDeleteInternal(
            string[] sources,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            sources ??= Array.Empty<string>();
            progress?.Report(new FileOperationProgressInfo
            {
                Phase = FileOperationPhase.Preparing,
                TotalEntries = 0,
                TotalBytes = 0
            });

            FileOperationPlan plan = BuildDeletePlan(sources, cancellationToken);
            return ExecutePlan(plan, false, progress, cancellationToken);
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

                foreach (string file in FileSystemService.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    if (cancellationToken.IsCancellationRequested)
                        return plan;
                    plan.Add(new FileOperationEntry(FileOperationAction.DeleteFile, file, null, SafeGetFileLength(file)));
                }

                List<string> directories = new List<string>(FileSystemService.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
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

            List<string> directories = new List<string>(FileSystemService.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories));
            foreach (string directory in directories)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                plan.Add(new FileOperationEntry(
                    FileOperationAction.CreateDirectory,
                    directory,
                    MapNestedTargetPath(sourceDirectory, targetDirectory, directory),
                    0));
            }

            List<string> files = new List<string>(FileSystemService.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories));
            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                string destinationFile = MapNestedTargetPath(sourceDirectory, targetDirectory, file);
                if (!TryResolveFileTarget(destinationFile, overwriteExistingFiles, conflictResolutions, out string resolvedDestinationFile))
                    continue;

                bool allowOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(destinationFile, conflictResolutions);
                plan.Add(new FileOperationEntry(FileOperationAction.CopyFile, file, resolvedDestinationFile, SafeGetFileLength(file), allowOverwrite));
            }

            if (operationType != FormCopy.Type.Move)
                return;

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                plan.Add(new FileOperationEntry(FileOperationAction.DeleteFile, file, null, SafeGetFileLength(file)));
            }

            foreach (string directory in directories.OrderByDescending(path => path.Length))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                plan.Add(new FileOperationEntry(FileOperationAction.DeleteDirectory, directory, null, 0));
            }

            plan.Add(new FileOperationEntry(FileOperationAction.DeleteDirectory, sourceDirectory, null, 0));
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
                return;

            long size = SafeGetFileLength(sourceFile);
            if (operationType == FormCopy.Type.Move && IsSameVolume(sourceFile, resolvedTargetFile))
            {
                bool allowOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(targetFile, conflictResolutions);
                plan.Add(new FileOperationEntry(FileOperationAction.MoveFile, sourceFile, resolvedTargetFile, size, allowOverwrite));
                return;
            }

            bool allowCopyOverwrite = overwriteExistingFiles || ShouldOverwriteConflict(targetFile, conflictResolutions);
            plan.Add(new FileOperationEntry(FileOperationAction.CopyFile, sourceFile, resolvedTargetFile, size, allowCopyOverwrite));
            if (operationType == FormCopy.Type.Move)
            {
                plan.Add(new FileOperationEntry(FileOperationAction.DeleteFile, sourceFile, null, size));
            }
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

            foreach (string file in FileSystemService.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
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

        private static FileOperationRunResult ExecutePlan(
            FileOperationPlan plan,
            bool overwriteExistingFiles,
            IProgress<FileOperationProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long completedBytes = 0;
            int completedEntries = 0;

            Report(progress, FileOperationPhase.Running, null, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);

            foreach (FileOperationEntry entry in plan.Entries)
            {
                if (cancellationToken.IsCancellationRequested)
                    return FileOperationRunResult.Cancelled;

                switch (entry.Action)
                {
                    case FileOperationAction.CreateDirectory:
                        FileSystemService.CreateDirectory(entry.DestinationPath);
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.DestinationPath, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        break;
                    case FileOperationAction.CopyFile:
                        if (!CopyFile(entry, progress, stopwatch, ref completedEntries, plan.TotalEntries, ref completedBytes, plan.TotalBytes, cancellationToken))
                            return FileOperationRunResult.Cancelled;
                        break;
                    case FileOperationAction.MoveFile:
                        FileSystemService.EnsureParentDirectory(entry.DestinationPath);
                        FileSystemService.MoveFile(entry.SourcePath, entry.DestinationPath);
                        completedBytes += entry.SizeBytes;
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        break;
                    case FileOperationAction.MoveDirectory:
                        FileSystemService.EnsureParentDirectory(entry.DestinationPath);
                        FileSystemService.MoveDirectory(entry.SourcePath, entry.DestinationPath);
                        completedBytes += entry.SizeBytes;
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        break;
                    case FileOperationAction.DeleteFile:
                        if (FileSystemService.FileExists(entry.SourcePath))
                        {
                            FileSystemService.DeleteFile(entry.SourcePath);
                        }
                        completedBytes += entry.SizeBytes;
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        break;
                    case FileOperationAction.DeleteDirectory:
                        if (FileSystemService.DirectoryExists(entry.SourcePath))
                        {
                            FileSystemService.DeleteDirectory(entry.SourcePath, false);
                        }
                        completedEntries++;
                        Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, plan.TotalEntries, completedBytes, plan.TotalBytes, stopwatch.Elapsed);
                        break;
                }
            }

            return FileOperationRunResult.Completed;
        }

        private static bool CopyFile(
            FileOperationEntry entry,
            IProgress<FileOperationProgressInfo> progress,
            Stopwatch stopwatch,
            ref int completedEntries,
            int totalEntries,
            ref long completedBytes,
            long totalBytes,
            CancellationToken cancellationToken)
        {
            FileSystemService.EnsureParentDirectory(entry.DestinationPath);

            using FileStream sourceStream = FileSystemService.OpenRead(entry.SourcePath);
            using FileStream destinationStream = FileSystemService.OpenWrite(entry.DestinationPath, entry.AllowOverwrite);

            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                destinationStream.Write(buffer, 0, bytesRead);
                completedBytes += bytesRead;
                Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, totalEntries, completedBytes, totalBytes, stopwatch.Elapsed);
            }

            completedEntries++;
            Report(progress, FileOperationPhase.Running, entry.SourcePath, completedEntries, totalEntries, completedBytes, totalBytes, stopwatch.Elapsed);
            return true;
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
            if (singleSource)
                return destinationPath;

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
            foreach (string file in FileSystemService.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
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

            public void Add(FileOperationEntry entry)
            {
                Entries.Add(entry);
                TotalEntries++;
                TotalBytes += entry.SizeBytes;
            }
        }

        private sealed class FileOperationEntry
        {
            public FileOperationEntry(FileOperationAction action, string sourcePath, string destinationPath, long sizeBytes, bool allowOverwrite = false)
            {
                Action = action;
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
                SizeBytes = sizeBytes;
                AllowOverwrite = allowOverwrite;
            }

            public FileOperationAction Action { get; }
            public string SourcePath { get; }
            public string DestinationPath { get; }
            public long SizeBytes { get; }
            public bool AllowOverwrite { get; }
        }

        private enum FileOperationAction
        {
            CreateDirectory,
            CopyFile,
            MoveFile,
            MoveDirectory,
            DeleteFile,
            DeleteDirectory
        }
    }
}
