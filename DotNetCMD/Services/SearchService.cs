using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class SearchService
    {
        public Task<IReadOnlyList<BrowserItemInfo>> SearchAsync(
            SearchQuery query,
            IProgress<SearchProgress> progress,
            CancellationToken cancellationToken)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            return Task.Run<IReadOnlyList<BrowserItemInfo>>(
                () => Search(query, progress, cancellationToken),
                cancellationToken);
        }

        private static IReadOnlyList<BrowserItemInfo> Search(
            SearchQuery query,
            IProgress<SearchProgress> progress,
            CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(query.ScopeDirectory ?? string.Empty);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);

            string pattern = string.IsNullOrWhiteSpace(query.NamePattern) ? "*" : query.NamePattern.Trim();
            Regex nameRegex = query.UseRegex
                ? new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2))
                : null;
            var results = new List<BrowserItemInfo>();
            var pending = new Stack<SearchDirectory>();
            pending.Push(new SearchDirectory(root, 0));
            int scanned = 0;
            int skipped = 0;

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SearchDirectory current = pending.Pop();
                progress?.Report(new SearchProgress(current.Path, scanned, results.Count, skipped));

                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(current.Path);
                }
                catch (Exception ex) when (IsSkippable(ex))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    foreach (string entryPath in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        scanned++;

                        FileAttributes attributes;
                        try
                        {
                            attributes = File.GetAttributes(entryPath);
                        }
                        catch (Exception ex) when (IsSkippable(ex))
                        {
                            skipped++;
                            continue;
                        }

                        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                        bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                        string name = Path.GetFileName(entryPath);

                        if (MatchesName(name, pattern, query.UseRegex, nameRegex) &&
                            (!isDirectory || query.IncludeDirectories) &&
                            (isDirectory || MatchesContent(entryPath, query.ContentText, cancellationToken)))
                        {
                            results.Add(CreateResult(entryPath, name, isDirectory));
                        }

                        if (isDirectory && query.Recursive && !isReparsePoint &&
                            (query.MaxDepth < 0 || current.Depth < query.MaxDepth))
                        {
                            pending.Push(new SearchDirectory(entryPath, current.Depth + 1));
                        }

                        if ((scanned & 0x7F) == 0)
                            progress?.Report(new SearchProgress(current.Path, scanned, results.Count, skipped));
                    }
                }
                catch (Exception ex) when (IsSkippable(ex))
                {
                    skipped++;
                }
            }

            results.Sort((left, right) =>
                StringComparer.CurrentCultureIgnoreCase.Compare(left.NativePath, right.NativePath));
            progress?.Report(new SearchProgress(root, scanned, results.Count, skipped));
            return results;
        }

        private static bool MatchesName(string name, string pattern, bool useRegex, Regex regex)
        {
            if (useRegex)
                return regex.IsMatch(name);

            string[] masks = pattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            return masks.Length == 0 || Array.Exists(masks, mask =>
                FileSystemName.MatchesSimpleExpression(mask.Trim(), name, ignoreCase: true));
        }

        private static bool MatchesContent(string path, string searchText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            try
            {
                using StreamReader reader = TextFileEncodingService.OpenReader(path, out _, 8192);
                char[] buffer = new char[8192];
                string tail = string.Empty;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (read == 0)
                        return false;

                    string block = tail + new string(buffer, 0, read);
                    if (block.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        return true;

                    int overlap = Math.Min(Math.Max(0, searchText.Length - 1), block.Length);
                    tail = overlap == 0 ? string.Empty : block.Substring(block.Length - overlap);
                }
            }
            catch (Exception ex) when (IsSkippable(ex) || ex is DecoderFallbackException)
            {
                return false;
            }
        }

        private static BrowserItemInfo CreateResult(string path, string name, bool isDirectory)
        {
            long? size = null;
            DateTime? modified = null;
            try
            {
                if (isDirectory)
                {
                    modified = Directory.GetLastWriteTime(path);
                }
                else
                {
                    var info = new FileInfo(path);
                    size = info.Length;
                    modified = info.LastWriteTime;
                }
            }
            catch (Exception ex) when (IsSkippable(ex))
            {
            }

            return new BrowserItemInfo
            {
                Name = name,
                Location = path,
                NativePath = path,
                IsDirectory = isDirectory,
                Size = size,
                Modified = modified
            };
        }

        private static bool IsSkippable(Exception exception)
        {
            return exception is UnauthorizedAccessException ||
                   exception is IOException ||
                   exception is System.Security.SecurityException ||
                   exception is NotSupportedException;
        }

        private readonly struct SearchDirectory
        {
            public SearchDirectory(string path, int depth)
            {
                Path = path;
                Depth = depth;
            }

            public string Path { get; }
            public int Depth { get; }
        }
    }

    internal sealed class SearchProgress
    {
        public SearchProgress(string currentPath, int scannedItems, int foundItems, int skippedDirectories)
        {
            CurrentPath = currentPath ?? string.Empty;
            ScannedItems = scannedItems;
            FoundItems = foundItems;
            SkippedDirectories = skippedDirectories;
        }

        public string CurrentPath { get; }
        public int ScannedItems { get; }
        public int FoundItems { get; }
        public int SkippedDirectories { get; }
    }
}
