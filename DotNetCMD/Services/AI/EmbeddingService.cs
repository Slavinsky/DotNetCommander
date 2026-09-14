using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class EmbeddingService
    {
        private readonly AiOrganizationService ollama;

        public EmbeddingService(AiOrganizationService ollama, string endpoint, string model)
        {
            this.ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
            Endpoint = (endpoint ?? string.Empty).Trim();
            Model = (model ?? string.Empty).Trim();
            if (Model.Length == 0) throw new ArgumentException("Embedding model is required.", nameof(model));
        }

        public string Endpoint { get; }
        public string Model { get; }

        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            float[][] result = await EmbedBatchAsync(new[] { text }, cancellationToken).ConfigureAwait(false);
            if (result.Length != 1) throw new InvalidOperationException("Ollama returned an unexpected number of embeddings.");
            return result[0];
        }

        public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        {
            float[][] vectors = await ollama.GetEmbeddingsAsync(Endpoint, Model, inputs, cancellationToken).ConfigureAwait(false);
            if (vectors.Length != inputs.Count) throw new InvalidOperationException("Ollama returned an unexpected number of embeddings.");
            return vectors;
        }

        public async Task<VectorEmbeddingProfile> CreateProfileAsync(int chunkSize, int chunkOverlap, CancellationToken cancellationToken)
        {
            IReadOnlyList<OllamaModelDescriptor> models = await ollama.GetModelsAsync(Endpoint, cancellationToken).ConfigureAwait(false);
            OllamaModelDescriptor descriptor = models.FirstOrDefault(item =>
                string.Equals(item.Name, Model, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Model, Model, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
                throw new InvalidOperationException(string.Format(Language.getString("vectorEmbeddingModelNotFoundFormat"), Model, Endpoint));
            if (descriptor.Capabilities?.Count > 0 && !descriptor.HasCapability("embedding"))
                throw new InvalidOperationException(string.Format(Language.getString("vectorEmbeddingModelUnsupportedFormat"), Model, Endpoint));
            return new VectorEmbeddingProfile
            {
                Endpoint = Endpoint,
                ModelName = descriptor.Name,
                ModelDigest = descriptor.Digest ?? string.Empty,
                ChunkSizeCharacters = chunkSize,
                ChunkOverlapCharacters = chunkOverlap
            };
        }
    }
}
