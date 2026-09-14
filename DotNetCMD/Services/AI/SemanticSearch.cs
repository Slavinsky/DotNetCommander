using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class SemanticSearch
    {
        private readonly VectorStore store;
        private readonly EmbeddingService embeddings;

        public SemanticSearch(VectorStore store, EmbeddingService embeddings)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        }

        public async Task<List<VectorSearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query) || topK <= 0) return new List<VectorSearchResult>();
            float[] queryVector = await embeddings.EmbedAsync(query, cancellationToken).ConfigureAwait(false);
            VectorStore.ValidateVector(queryVector, store.Profile.VectorDimension);
            return store.Search(queryVector, topK);
        }
    }
}
