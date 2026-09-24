using System;
using System.Collections.Generic;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class SearchQueryHistoryTests
    {
        [Fact]
        public void Add_NewQueryGoesFirst()
        {
            var history = new SearchQueryHistory();
            history.Add(MakeQuery("first"));
            history.Add(MakeQuery("second"));

            Assert.Equal(2, history.Items.Count);
            Assert.Equal("second", history.Items[0].NamePattern);
            Assert.Equal("first", history.Items[1].NamePattern);
        }

        [Fact]
        public void Add_DuplicateMovesToFront_WithoutDuplicates()
        {
            var history = new SearchQueryHistory();
            history.Add(MakeQuery("a"));
            history.Add(MakeQuery("b"));
            history.Add(MakeQuery("a"));

            Assert.Equal(2, history.Items.Count);
            Assert.Equal("a", history.Items[0].NamePattern);
            Assert.Equal("b", history.Items[1].NamePattern);
        }

        [Fact]
        public void Add_DifferentFilters_AreSeparateEntries()
        {
            var history = new SearchQueryHistory();
            SearchQuery first = MakeQuery("same");
            first.MinSizeBytes = 1024;
            SearchQuery second = MakeQuery("same");
            second.MinSizeBytes = 2048;

            history.Add(first);
            history.Add(second);

            Assert.Equal(2, history.Items.Count);
        }

        [Fact]
        public void Add_CapsAtCapacity_DroppingOldestFirst()
        {
            var history = new SearchQueryHistory();
            for (int index = 1; index <= SearchQueryHistory.Capacity + 2; index++)
            {
                history.Add(MakeQuery("p" + index));
            }

            Assert.Equal(SearchQueryHistory.Capacity, history.Items.Count);
            Assert.Equal("p" + (SearchQueryHistory.Capacity + 2), history.Items[0].NamePattern);
            Assert.Equal("p3", history.Items[SearchQueryHistory.Capacity - 1].NamePattern);
        }

        [Fact]
        public void Add_StoresCopy_ModifyingOriginalDoesNotAffectHistory()
        {
            var history = new SearchQueryHistory();
            SearchQuery query = MakeQuery("original");
            history.Add(query);
            query.NamePattern = "changed";

            Assert.Equal("original", history.Items[0].NamePattern);
        }

        [Fact]
        public void Clear_RemovesAll()
        {
            var history = new SearchQueryHistory();
            history.Add(MakeQuery("a"));
            history.Add(MakeQuery("b"));

            history.Clear();

            Assert.Empty(history.Items);
        }

        [Fact]
        public void Add_Null_Throws()
        {
            var history = new SearchQueryHistory();
            Assert.Throws<ArgumentNullException>(() => history.Add(null));
        }

        [Fact]
        public void GetDisplayLabel_IncludesPatternContentAndScope()
        {
            SearchQuery query = MakeQuery("*.cs");
            query.ContentText = "TODO";

            string label = SearchQueryHistory.GetDisplayLabel(query);

            Assert.Contains("*.cs", label);
            Assert.Contains("TODO", label);
            Assert.Contains(query.ScopeDirectory, label);
        }

        [Fact]
        public void GetSignature_DiffersByEveryFilterField()
        {
            SearchQuery baseline = MakeQuery("pattern");

            var variants = new List<SearchQuery>
            {
                MakeQuery("other"),
                With(q => q.ScopeDirectory = "C:\\other"),
                With(q => q.ContentText = "text"),
                With(q => q.Recursive = false),
                With(q => q.IncludeDirectories = false),
                With(q => q.UseRegex = true),
                With(q => q.MaxDepth = 3),
                With(q => q.MinSizeBytes = 10),
                With(q => q.MaxSizeBytes = 10),
                With(q => q.ModifiedFrom = new DateTime(2026, 1, 1)),
                With(q => q.ModifiedTo = new DateTime(2026, 1, 2))
            };

            string baselineSignature = SearchQueryHistory.GetSignature(baseline);
            foreach (SearchQuery variant in variants)
            {
                Assert.NotEqual(baselineSignature, SearchQueryHistory.GetSignature(variant));
            }
        }

        private static SearchQuery MakeQuery(string pattern)
        {
            return new SearchQuery
            {
                ScopeDirectory = "C:\\base",
                NamePattern = pattern
            };
        }

        private static SearchQuery With(Action<SearchQuery> mutate)
        {
            SearchQuery query = MakeQuery("pattern");
            mutate(query);
            return query;
        }
    }
}
