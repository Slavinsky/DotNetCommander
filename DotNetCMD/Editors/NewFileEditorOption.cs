namespace DotNetCommander
{
    internal sealed class NewFileEditorOption
    {
        public NewFileEditorOption(string key, string displayName, string defaultExtension)
        {
            Key = key;
            DisplayName = displayName;
            DefaultExtension = defaultExtension;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string DefaultExtension { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
