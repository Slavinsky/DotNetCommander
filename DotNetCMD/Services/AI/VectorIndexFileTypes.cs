namespace DotNetCommander
{
    internal static class VectorIndexFileTypes
    {
        public static bool IsEnabled(string path)
        {
            switch (FileTypeClassifier.Classify(path))
            {
                case FileContentKind.Text:
                    return Properties.Settings.Default.VectorIndexTextEnabled;
                case FileContentKind.Markdown:
                    return Properties.Settings.Default.VectorIndexMarkdownEnabled;
                case FileContentKind.Csv:
                    return Properties.Settings.Default.VectorIndexCsvEnabled;
                default:
                    return false;
            }
        }
    }
}
