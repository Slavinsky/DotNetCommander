using System;

namespace DotNetCommander
{
    internal enum VectorSimilarityMetric : byte
    {
        DotProduct = 1,
        Cosine = 2
    }

    internal sealed class VectorEmbeddingProfile
    {
        public string Provider { get; set; } = "ollama";
        public string Endpoint { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelDigest { get; set; } = string.Empty;
        public int VectorDimension { get; set; }
        public VectorSimilarityMetric SimilarityMetric { get; set; } = VectorSimilarityMetric.DotProduct;
        public bool VectorsNormalized { get; set; } = true;
        public int ChunkerVersion { get; set; } = 1;
        public int ChunkSizeCharacters { get; set; } = 2000;
        public int ChunkOverlapCharacters { get; set; } = 200;
        public int TextExtractorVersion { get; set; } = 1;

        public bool IsEmbeddingCompatibleWith(VectorEmbeddingProfile other)
        {
            return other != null &&
                string.Equals(Provider, other.Provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ModelName, other.ModelName, StringComparison.OrdinalIgnoreCase) &&
                IdentityMatches(other) &&
                (VectorDimension == 0 || other.VectorDimension == 0 || VectorDimension == other.VectorDimension) &&
                SimilarityMetric == other.SimilarityMetric &&
                VectorsNormalized == other.VectorsNormalized;
        }

        public bool IsIndexingCompatibleWith(VectorEmbeddingProfile other)
        {
            return IsEmbeddingCompatibleWith(other) &&
                ChunkerVersion == other.ChunkerVersion &&
                ChunkSizeCharacters == other.ChunkSizeCharacters &&
                ChunkOverlapCharacters == other.ChunkOverlapCharacters &&
                TextExtractorVersion == other.TextExtractorVersion;
        }

        private bool IdentityMatches(VectorEmbeddingProfile other)
        {
            bool hasDigest = !string.IsNullOrWhiteSpace(ModelDigest);
            bool otherHasDigest = !string.IsNullOrWhiteSpace(other.ModelDigest);
            if (hasDigest || otherHasDigest)
                return hasDigest && otherHasDigest && string.Equals(ModelDigest, other.ModelDigest, StringComparison.OrdinalIgnoreCase);
            return string.Equals(Endpoint?.TrimEnd('/'), other.Endpoint?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
