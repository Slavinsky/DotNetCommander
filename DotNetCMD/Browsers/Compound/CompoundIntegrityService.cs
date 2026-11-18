using OpenMcdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal enum CompoundIntegrityStatus
    {
        NotRun,
        Passed,
        IssuesFound,
        Incomplete,
        Failed,
        Cancelled
    }

    internal sealed class CompoundIntegrityIssue
    {
        public CompoundIntegrityIssue(string stageKey, string path, string message)
        {
            StageKey = stageKey ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string StageKey { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal sealed class CompoundIntegrityPass
    {
        public CompoundIntegrityPass(CompoundIntegrityStatus status, string error = null)
        {
            Status = status;
            Error = error ?? string.Empty;
        }

        public CompoundIntegrityStatus Status { get; set; }
        public string Error { get; set; }
        public int EntriesVisited { get; set; }
        public int StreamsFullyRead { get; set; }
        public int StreamsPartiallyRead { get; set; }
        public long BytesRead { get; set; }
        public int OmittedIssues { get; set; }
        public List<CompoundIntegrityIssue> Issues { get; } = new List<CompoundIntegrityIssue>();
    }

    internal sealed class CompoundIntegrityReport
    {
        public CompoundIntegrityReport(string path, CompoundIntegrityPass strict, CompoundIntegrityPass lenient)
        {
            Path = path ?? string.Empty;
            Strict = strict;
            Lenient = lenient;
        }

        public string Path { get; }
        public CompoundIntegrityPass Strict { get; }
        public CompoundIntegrityPass Lenient { get; }
    }

    internal sealed class CompoundIntegrityProgress
    {
        public CompoundIntegrityProgress(bool strict, int entries, int completeStreams, long bytesRead)
        {
            IsStrict = strict;
            EntriesVisited = entries;
            StreamsFullyRead = completeStreams;
            BytesRead = bytesRead;
        }

        public bool IsStrict { get; }
        public int EntriesVisited { get; }
        public int StreamsFullyRead { get; }
        public long BytesRead { get; }
    }

    internal static class CompoundIntegrityService
    {
        private const int MaximumEntries = 200000;
        private const int MaximumDepth = 256;
        private const int MaximumStreams = 4096;
        private const int MaximumIssues = 100;
        private const long MaximumFileBytes = 256L * 1024 * 1024;
        private const long MaximumStreamBytes = 64L * 1024 * 1024;
        private const long MaximumTotalBytes = 256L * 1024 * 1024;
        private static readonly TimeSpan MaximumScanTime = TimeSpan.FromSeconds(10);

        public static Task<CompoundIntegrityReport> InspectAsync(
            string path,
            Exception strictCatalogError,
            IProgress<CompoundIntegrityProgress> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => Inspect(path, strictCatalogError, progress, cancellationToken),
                cancellationToken);
        }

        private static CompoundIntegrityReport Inspect(
            string path,
            Exception strictCatalogError,
            IProgress<CompoundIntegrityProgress> progress,
            CancellationToken cancellationToken)
        {
            if (strictCatalogError != null)
            {
                var strictFailure = new CompoundIntegrityPass(CompoundIntegrityStatus.Failed, strictCatalogError.Message);
                AddIssue(strictFailure, "compoundIntegrityStrictCatalog", string.Empty, strictCatalogError.Message);
                CompoundIntegrityPass lenient = Scan(path, strict: false, progress, cancellationToken);
                return new CompoundIntegrityReport(path, strictFailure, lenient);
            }

            CompoundIntegrityPass strict = Scan(path, strict: true, progress, cancellationToken);
            CompoundIntegrityPass lenientPass = strict.Status == CompoundIntegrityStatus.Failed ||
                strict.Status == CompoundIntegrityStatus.IssuesFound
                    ? Scan(path, strict: false, progress, cancellationToken)
                    : new CompoundIntegrityPass(CompoundIntegrityStatus.NotRun);
            return new CompoundIntegrityReport(path, strict, lenientPass);
        }

        private static CompoundIntegrityPass Scan(
            string path,
            bool strict,
            IProgress<CompoundIntegrityProgress> progress,
            CancellationToken cancellationToken)
        {
            var report = new CompoundIntegrityPass(CompoundIntegrityStatus.Passed);
            var timer = Stopwatch.StartNew();
            long totalBytes = 0;
            long lastProgressBytes = 0;
            int streamsSeen = 0;
            bool incomplete = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    report.Status = CompoundIntegrityStatus.Failed;
                    report.Error = "File not found.";
                    AddIssue(report, "compoundIntegrityFile", path, report.Error);
                    return report;
                }

                long fileLength = new FileInfo(path).Length;
                if (fileLength < 0 || fileLength > MaximumFileBytes)
                {
                    report.Status = CompoundIntegrityStatus.Incomplete;
                    AddIssue(report, "compoundIntegrityLimit", path, Language.getString("compoundIntegrityFileLimit"));
                    return report;
                }

                using RootStorage root = RootStorage.OpenRead(
                    path,
                    strict ? StorageModeFlags.StrictValidation : StorageModeFlags.None);
                cancellationToken.ThrowIfCancellationRequested();
                if (timer.Elapsed >= MaximumScanTime)
                {
                    incomplete = true;
                    AddIssue(report, "compoundIntegrityLimit", string.Empty, Language.getString("compoundIntegrityTimeLimit"));
                    return Finish(report, incomplete);
                }

                var pending = new Stack<StorageFrame>();
                pending.Push(new StorageFrame(root, string.Empty, 0));
                while (pending.Count > 0 && !incomplete)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (timer.Elapsed >= MaximumScanTime)
                    {
                        incomplete = true;
                        AddIssue(report, "compoundIntegrityLimit", string.Empty, Language.getString("compoundIntegrityTimeLimit"));
                        break;
                    }

                    StorageFrame frame = pending.Pop();
                    try
                    {
                        foreach (EntryInfo entry in frame.Storage.EnumerateEntries())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (timer.Elapsed >= MaximumScanTime || report.EntriesVisited >= MaximumEntries)
                            {
                                incomplete = true;
                                AddIssue(report, "compoundIntegrityLimit", frame.Path,
                                    timer.Elapsed >= MaximumScanTime
                                        ? Language.getString("compoundIntegrityTimeLimit")
                                        : Language.getString("compoundIntegrityEntryLimit"));
                                break;
                            }

                            report.EntriesVisited++;
                            string entryPath = string.IsNullOrEmpty(frame.Path)
                                ? entry.Name
                                : frame.Path + "/" + entry.Name;
                            if (entry.Type == EntryType.Storage)
                            {
                                if (frame.Depth >= MaximumDepth)
                                {
                                    incomplete = true;
                                    AddIssue(report, "compoundIntegrityLimit", entryPath, Language.getString("compoundIntegrityDepthLimit"));
                                    break;
                                }

                                try
                                {
                                    pending.Push(new StorageFrame(frame.Storage.OpenStorage(entry.Name), entryPath, frame.Depth + 1));
                                }
                                catch (Exception ex) when (IsReadFailure(ex))
                                {
                                    AddIssue(report, "compoundIntegrityStorage", entryPath, ex.Message);
                                }
                                continue;
                            }

                            if (++streamsSeen > MaximumStreams)
                            {
                                incomplete = true;
                                AddIssue(report, "compoundIntegrityLimit", entryPath, Language.getString("compoundIntegrityStreamCountLimit"));
                                break;
                            }

                            if (entry.Length < 0)
                            {
                                AddIssue(report, "compoundIntegrityStreamOpen", entryPath, Language.getString("compoundIntegrityInvalidSize"));
                                continue;
                            }

                            long expected = entry.Length;
                            if (expected > MaximumStreamBytes || (expected > 0 && totalBytes >= MaximumTotalBytes))
                            {
                                incomplete = true;
                                AddIssue(report, "compoundIntegrityLimit", entryPath, Language.getString("compoundIntegrityStreamLimit"));
                                break;
                            }

                            CfbStream stream;
                            try
                            {
                                stream = frame.Storage.OpenStream(entry.Name);
                            }
                            catch (Exception ex) when (IsReadFailure(ex))
                            {
                                AddIssue(report, "compoundIntegrityStreamOpen", entryPath, ex.Message);
                                continue;
                            }

                            try
                            {
                                using (stream)
                                {
                                    long streamLimit = Math.Min(expected, Math.Min(MaximumStreamBytes, MaximumTotalBytes - totalBytes));
                                    byte[] buffer = new byte[81920];
                                    long streamBytes = 0;
                                    bool reachedEnd = false;
                                    bool timeExceeded = false;
                                    while (streamBytes < streamLimit)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        if (timer.Elapsed >= MaximumScanTime)
                                        {
                                            incomplete = true;
                                            timeExceeded = true;
                                            break;
                                        }

                                        int requested = (int)Math.Min(buffer.Length, streamLimit - streamBytes);
                                        int read = stream.Read(buffer, 0, requested);
                                        if (read == 0)
                                        {
                                            reachedEnd = true;
                                            break;
                                        }

                                        streamBytes += read;
                                        totalBytes += read;
                                        report.BytesRead += read;
                                        if (report.BytesRead - lastProgressBytes >= 1024 * 1024)
                                        {
                                            progress?.Report(new CompoundIntegrityProgress(strict, report.EntriesVisited, report.StreamsFullyRead, report.BytesRead));
                                            lastProgressBytes = report.BytesRead;
                                        }
                                    }

                                    if (timeExceeded)
                                    {
                                        report.StreamsPartiallyRead++;
                                        AddIssue(report, "compoundIntegrityLimit", entryPath, Language.getString("compoundIntegrityTimeLimit"));
                                    }
                                    else if (streamBytes < expected && reachedEnd)
                                    {
                                        report.StreamsPartiallyRead++;
                                        AddIssue(report, "compoundIntegrityStreamRead", entryPath, Language.getString("compoundIntegrityShortRead"));
                                    }
                                    else if (streamBytes == expected)
                                    {
                                        report.StreamsFullyRead++;
                                    }
                                    else
                                    {
                                        report.StreamsPartiallyRead++;
                                        incomplete = true;
                                        AddIssue(report, "compoundIntegrityLimit", entryPath, Language.getString("compoundIntegrityStreamLimit"));
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex) when (IsReadFailure(ex))
                            {
                                report.StreamsPartiallyRead++;
                                AddIssue(report, "compoundIntegrityStreamRead", entryPath, ex.Message);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsReadFailure(ex))
                    {
                        AddIssue(report, "compoundIntegrityStorage", frame.Path, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                report.Status = CompoundIntegrityStatus.Cancelled;
                return report;
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                report.Status = CompoundIntegrityStatus.Failed;
                report.Error = ex.Message;
                AddIssue(report, "compoundIntegrityOpen", path, ex.Message);
                return report;
            }

            return Finish(report, incomplete);
        }

        private static CompoundIntegrityPass Finish(CompoundIntegrityPass report, bool incomplete)
        {
            if (incomplete)
                report.Status = CompoundIntegrityStatus.Incomplete;
            else if (report.Issues.Count > 0)
                report.Status = CompoundIntegrityStatus.IssuesFound;
            else
                report.Status = CompoundIntegrityStatus.Passed;

            return report;
        }

        private static void AddIssue(CompoundIntegrityPass report, string stageKey, string path, string message)
        {
            if (report.Issues.Count >= MaximumIssues)
            {
                report.OmittedIssues++;
                return;
            }

            report.Issues.Add(new CompoundIntegrityIssue(stageKey, path, message));
        }

        private static bool IsReadFailure(Exception ex)
        {
            return ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException ||
                ex is NotSupportedException ||
                ex is ArgumentException ||
                ex is FormatException ||
                ex is InvalidOperationException ||
                ex is OverflowException;
        }

        private readonly struct StorageFrame
        {
            public StorageFrame(Storage storage, string path, int depth)
            {
                Storage = storage;
                Path = path;
                Depth = depth;
            }

            public Storage Storage { get; }
            public string Path { get; }
            public int Depth { get; }
        }
    }
}
