using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class FileIndexer
    {
        private readonly VectorStore store;
        private readonly EmbeddingService embeddings;
        private readonly long maximumFileBytes;

        public FileIndexer(VectorStore store, EmbeddingService embeddings, long maximumFileBytes)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
            this.maximumFileBytes = maximumFileBytes;
        }

        public async Task<bool> IndexFileAsync(string filePath, CancellationToken cancellationToken)
        {
            var info = new FileInfo(filePath);
            if (!info.Exists || (maximumFileBytes > 0 && info.Length > maximumFileBytes) || !VectorIndexFileTypes.IsEnabled(filePath)) return false;
            long ticks = info.LastWriteTimeUtc.Ticks;
            if (store.IsFileUpToDate(filePath, info.Length, ticks)) return false;

            string text = await Task.Run(() => TextFileEncodingService.ReadAllText(filePath, out Encoding _), cancellationToken).ConfigureAwait(false);
            List<TextChunk> chunks = SplitText(text, store.Profile.ChunkSizeCharacters, store.Profile.ChunkOverlapCharacters);
            if (chunks.Count == 0) return false;
            var inputs = new List<string>(chunks.Count);
            foreach (TextChunk chunk in chunks) inputs.Add(chunk.Text);
            var vectorList = new List<float[]>(chunks.Count);
            const int embeddingBatchSize = 32;
            for (int offset = 0; offset < inputs.Count; offset += embeddingBatchSize)
            {
                int count = Math.Min(embeddingBatchSize, inputs.Count - offset);
                float[][] batch = await embeddings.EmbedBatchAsync(inputs.GetRange(offset, count), cancellationToken).ConfigureAwait(false);
                vectorList.AddRange(batch);
            }
            float[][] vectors = vectorList.ToArray();
            if (vectors.Length != chunks.Count) throw new InvalidDataException("Embedding count does not match chunk count.");

            if (store.Profile.VectorDimension == 0) store.Profile.VectorDimension = vectors[0].Length;
            string relativePath = Path.GetRelativePath(store.RootPath, info.FullName);
            var replacements = new List<VectorRecord>(chunks.Count);
            for (int index = 0; index < chunks.Count; index++)
            {
                VectorStore.ValidateVector(vectors[index], store.Profile.VectorDimension);
                float[] vector = store.Profile.VectorsNormalized ? VectorStore.Normalize(vectors[index]) : vectors[index];
                replacements.Add(new VectorRecord
                {
                    RelativeFilePath = relativePath,
                    FileSize = info.Length,
                    LastWriteTimeUtcTicks = ticks,
                    ChunkIndex = index,
                    ChunkStartCharacter = chunks[index].Start,
                    Text = chunks[index].Text,
                    Vector = vector
                });
            }
            cancellationToken.ThrowIfCancellationRequested();
            store.ReplaceFile(info.FullName, replacements);
            return true;
        }

        private static List<TextChunk> SplitText(string text, int chunkSize, int overlap)
        {
            var result = new List<TextChunk>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            int step = chunkSize - overlap;
            for (int start = 0; start < text.Length; start += step)
            {
                int length = Math.Min(chunkSize, text.Length - start);
                string value = text.Substring(start, length).Trim();
                if (value.Length > 0) result.Add(new TextChunk(start, value));
                if (start + length >= text.Length) break;
            }
            return result;
        }

        private readonly struct TextChunk
        {
            public TextChunk(int start, string text) { Start = start; Text = text; }
            public int Start { get; }
            public string Text { get; }
        }
    }
}
