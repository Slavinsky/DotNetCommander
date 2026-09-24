using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DotNetCommander
{
    internal sealed class SearchQueryHistory
    {
        public const int Capacity = 10;
        private static readonly char Separator = '\u001F';

        public static SearchQueryHistory Session { get; } = new SearchQueryHistory();

        private readonly List<SearchQuery> items = new List<SearchQuery>();

        public IReadOnlyList<SearchQuery> Items => items;

        public void Add(SearchQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            string signature = GetSignature(query);
            items.RemoveAll(candidate => string.Equals(GetSignature(candidate), signature, StringComparison.Ordinal));
            items.Insert(0, query.Clone());
            if (items.Count > Capacity)
            {
                items.RemoveRange(Capacity, items.Count - Capacity);
            }
        }

        public void Clear()
        {
            items.Clear();
        }

        public static string GetDisplayLabel(SearchQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var label = new StringBuilder(query.NamePattern);
            if (!string.IsNullOrEmpty(query.ContentText))
            {
                label.Append(" + \"").Append(query.ContentText).Append('"');
            }
            label.Append(" | ").Append(query.ScopeDirectory);
            return label.ToString();
        }

        internal static string GetSignature(SearchQuery query)
        {
            var signature = new StringBuilder();
            signature.Append(query.ScopeDirectory).Append(Separator);
            signature.Append(query.NamePattern).Append(Separator);
            signature.Append(query.ContentText).Append(Separator);
            signature.Append(query.Recursive ? '1' : '0').Append(Separator);
            signature.Append(query.IncludeDirectories ? '1' : '0').Append(Separator);
            signature.Append(query.UseRegex ? '1' : '0').Append(Separator);
            signature.Append(query.MaxDepth.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            signature.Append(query.MinSizeBytes?.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            signature.Append(query.MaxSizeBytes?.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            signature.Append(query.ModifiedFrom?.Ticks.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            signature.Append(query.ModifiedTo?.Ticks.ToString(CultureInfo.InvariantCulture));
            return signature.ToString();
        }
    }
}
