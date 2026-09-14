namespace DotNetCommander
{
    internal sealed class VectorRecord
    {
        public string RelativeFilePath { get; init; } = string.Empty;
        public long FileSize { get; init; }
        public long LastWriteTimeUtcTicks { get; init; }
        public int ChunkIndex { get; init; }
        public int ChunkStartCharacter { get; init; }
        public string Text { get; init; } = string.Empty;
        public float[] Vector { get; init; } = System.Array.Empty<float>();
    }

    internal sealed class VectorSearchResult
    {
        public string FilePath { get; init; } = string.Empty;
        public int ChunkIndex { get; init; }
        public int ChunkStartCharacter { get; init; }
        public string Text { get; init; } = string.Empty;
        public float Score { get; init; }
    }
}
