namespace DotNetCommander
{
    internal sealed class SearchQuery
    {
        public string ScopeDirectory { get; set; } = string.Empty;
        public string NamePattern { get; set; } = "*";
        public string ContentText { get; set; } = string.Empty;
        public bool Recursive { get; set; } = true;
        public bool IncludeDirectories { get; set; } = true;
        public bool UseRegex { get; set; }
        public int MaxDepth { get; set; } = -1;

        public SearchQuery Clone()
        {
            return (SearchQuery)MemberwiseClone();
        }
    }
}
