using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class SearchServiceTests
    {
        private readonly SearchService service = new SearchService();

        private async Task<IReadOnlyList<BrowserItemInfo>> SearchAsync(
            string root,
            Action<SearchQuery> configure = null,
            CancellationToken cancellationToken = default)
        {
            var query = new SearchQuery { ScopeDirectory = root };
            configure?.Invoke(query);
            return await service.SearchAsync(query, null, cancellationToken);
        }

        [Fact]
        public async Task SearchAsync_WithoutFilters_ReturnsFilesAndDirectories()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("alpha.txt"), "alpha");
            Directory.CreateDirectory(directory.GetPath("sub"));

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root);

            Assert.Contains(results, item => item.Name == "alpha.txt" && !item.IsDirectory);
            Assert.Contains(results, item => item.Name == "sub" && item.IsDirectory);
        }

        [Fact]
        public async Task SearchAsync_SizeFilter_ExcludesFilesOutsideRangeAndDirectories()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllBytes(directory.GetPath("small.bin"), new byte[100]);
            File.WriteAllBytes(directory.GetPath("large.bin"), new byte[5000]);
            Directory.CreateDirectory(directory.GetPath("folder"));

            IReadOnlyList<BrowserItemInfo> withMinimum = await SearchAsync(directory.Root, query =>
                query.MinSizeBytes = 1000);
            Assert.Contains(withMinimum, item => item.Name == "large.bin");
            Assert.DoesNotContain(withMinimum, item => item.Name == "small.bin");
            Assert.DoesNotContain(withMinimum, item => item.Name == "folder");

            IReadOnlyList<BrowserItemInfo> withMaximum = await SearchAsync(directory.Root, query =>
                query.MaxSizeBytes = 200);
            Assert.Contains(withMaximum, item => item.Name == "small.bin");
            Assert.DoesNotContain(withMaximum, item => item.Name == "large.bin");
            Assert.DoesNotContain(withMaximum, item => item.Name == "folder");
        }

        [Fact]
        public async Task SearchAsync_SizeRange_KeepsOnlyFilesInsideRange()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllBytes(directory.GetPath("tiny.bin"), new byte[10]);
            File.WriteAllBytes(directory.GetPath("medium.bin"), new byte[1000]);
            File.WriteAllBytes(directory.GetPath("huge.bin"), new byte[100000]);

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
            {
                query.MinSizeBytes = 500;
                query.MaxSizeBytes = 5000;
            });

            Assert.Single(results, item => item.Name == "medium.bin");
        }

        [Fact]
        public async Task SearchAsync_DateFilter_ExcludesOlderAndNewerEntries()
        {
            using var directory = new TemporaryDirectory();
            string recent = directory.GetPath("recent.txt");
            string old = directory.GetPath("old.txt");
            File.WriteAllText(recent, "recent");
            File.WriteAllText(old, "old");
            File.SetLastWriteTime(recent, DateTime.Now);
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-10));

            DateTime threeDaysAgo = DateTime.Today.AddDays(-3);
            IReadOnlyList<BrowserItemInfo> fromResults = await SearchAsync(directory.Root, query =>
                query.ModifiedFrom = threeDaysAgo);
            Assert.Contains(fromResults, item => item.Name == "recent.txt");
            Assert.DoesNotContain(fromResults, item => item.Name == "old.txt");

            DateTime fiveDaysAgoEndOfDay = DateTime.Today.AddDays(-5).AddDays(1).AddTicks(-1);
            IReadOnlyList<BrowserItemInfo> toResults = await SearchAsync(directory.Root, query =>
                query.ModifiedTo = fiveDaysAgoEndOfDay);
            Assert.Contains(toResults, item => item.Name == "old.txt");
            Assert.DoesNotContain(toResults, item => item.Name == "recent.txt");
        }

        [Fact]
        public async Task SearchAsync_ContentFilter_MatchesCaseInsensitiveText()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("hit.txt"), "This document mentions the Needle somewhere");
            File.WriteAllText(directory.GetPath("miss.txt"), "nothing relevant in this file");

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
                query.ContentText = "needle");

            Assert.Single(results, item => item.Name == "hit.txt");
        }

        [Fact]
        public async Task SearchAsync_NameMask_ReturnsOnlyMatchingNames()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("report-01.log"), "1");
            File.WriteAllText(directory.GetPath("report-02.txt"), "2");
            File.WriteAllText(directory.GetPath("other.md"), "3");

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
                query.NamePattern = "report-*");

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, item => item.Name == "other.md");
        }

        [Fact]
        public async Task SearchAsync_RegexNamePattern_MatchesExactExpression()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("report_1.txt"), "1");
            File.WriteAllText(directory.GetPath("report_x.txt"), "x");
            File.WriteAllText(directory.GetPath("notes.txt"), "n");

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
            {
                query.UseRegex = true;
                query.NamePattern = @"^report_\d+\.txt$";
            });

            Assert.Single(results, item => item.Name == "report_1.txt");
        }

        [Fact]
        public async Task SearchAsync_MaxDepth_DoesNotDescendDeeperThanRequested()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("root.txt"), "root");
            Directory.CreateDirectory(directory.GetPath("one"));
            File.WriteAllText(directory.GetPath("one/two.txt"), "two");
            Directory.CreateDirectory(directory.GetPath("one/deep"));
            File.WriteAllText(directory.GetPath("one/deep/three.txt"), "three");

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
            {
                query.MaxDepth = 1;
                query.IncludeDirectories = false;
            });

            Assert.Contains(results, item => item.Name == "root.txt");
            Assert.Contains(results, item => item.Name == "two.txt");
            Assert.DoesNotContain(results, item => item.Name == "three.txt");
        }

        [Fact]
        public async Task SearchAsync_CombinedFilters_ApplyAllConditions()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllBytes(directory.GetPath("keep.log"), new byte[1000]);
            File.WriteAllBytes(directory.GetPath("skip-small.log"), new byte[10]);
            File.WriteAllBytes(directory.GetPath("keep.txt"), new byte[1000]);

            IReadOnlyList<BrowserItemInfo> results = await SearchAsync(directory.Root, query =>
            {
                query.NamePattern = "*.log";
                query.MinSizeBytes = 500;
            });

            Assert.Single(results, item => item.Name == "keep.log");
        }

        [Fact]
        public async Task SearchAsync_PreCancelledToken_ThrowsOperationCanceled()
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(directory.GetPath("alpha.txt"), "alpha");
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SearchAsync(directory.Root, null, cancellation.Token));
        }
    }
}
