using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class VectorStore
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DNCVECTOR");
        private const int FormatVersion = 2;
        private const int MaximumRecords = 10_000_000;
        private const int MaximumStringBytes = 64 * 1024 * 1024;
        private const int MaximumVectorDimension = 1_000_000;
        private readonly object syncRoot = new object();
        private List<VectorRecord> records = new List<VectorRecord>();

        public VectorStore(string storeId, string rootPath, VectorEmbeddingProfile profile)
        {
            StoreId = storeId ?? throw new ArgumentNullException(nameof(storeId));
            RootPath = VectorStoreRegistry.NormalizeRoot(rootPath);
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
        }

        public string StoreId { get; }
        public string RootPath { get; }
        public VectorEmbeddingProfile Profile { get; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime UpdatedUtc { get; private set; }
        public int Count { get { lock (syncRoot) return records.Count; } }

        public long EstimateSerializedSize()
        {
            lock (syncRoot)
            {
                long size = 1024;
                foreach (VectorRecord record in records)
                {
                    size += 64L + Encoding.UTF8.GetByteCount(record.RelativeFilePath) + Encoding.UTF8.GetByteCount(record.Text);
                    size += (long)record.Vector.Length * sizeof(float);
                }
                return size;
            }
        }

        public bool IsFileUpToDate(string filePath, long size, long lastWriteTimeUtcTicks)
        {
            string relativePath = GetRelativePath(filePath);
            lock (syncRoot)
            {
                List<VectorRecord> matches = records.Where(item => PathEquals(item.RelativeFilePath, relativePath)).ToList();
                return matches.Count > 0 && matches.All(item => item.FileSize == size && item.LastWriteTimeUtcTicks == lastWriteTimeUtcTicks);
            }
        }

        public void ReplaceFile(string filePath, IReadOnlyList<VectorRecord> replacements)
        {
            string relativePath = GetRelativePath(filePath);
            ValidateRecords(replacements, relativePath);
            lock (syncRoot)
            {
                var updated = records.Where(item => !PathEquals(item.RelativeFilePath, relativePath)).ToList();
                updated.AddRange(replacements);
                records = updated;
                UpdatedUtc = DateTime.UtcNow;
            }
        }

        public void RemoveMissingFiles()
        {
            lock (syncRoot)
            {
                records = records.Where(item => File.Exists(GetFullPath(item.RelativeFilePath))).ToList();
                UpdatedUtc = DateTime.UtcNow;
            }
        }

        public void RemoveFilesWhere(Func<string, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            lock (syncRoot)
            {
                var removedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string relativePath in records.Select(item => item.RelativeFilePath).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (predicate(GetFullPath(relativePath))) removedPaths.Add(relativePath);
                }
                if (removedPaths.Count > 0)
                {
                    records = records.Where(item => !removedPaths.Contains(item.RelativeFilePath)).ToList();
                    UpdatedUtc = DateTime.UtcNow;
                }
            }
        }

        public List<VectorSearchResult> Search(float[] query, int topK, string scopePath = null)
        {
            ValidateVector(query, Profile.VectorDimension);
            if (topK <= 0)
                return new List<VectorSearchResult>();

            List<VectorRecord> snapshot;
            lock (syncRoot) snapshot = records.ToList();
            if (Profile.VectorsNormalized)
                query = Normalize(query);

            string normalizedScope = string.IsNullOrWhiteSpace(scopePath) ? null : VectorStoreRegistry.NormalizeRoot(scopePath);
            string scopePrefix = normalizedScope == null ? null : (normalizedScope.EndsWith(Path.DirectorySeparatorChar) ? normalizedScope : normalizedScope + Path.DirectorySeparatorChar);
            return snapshot.Select(item => new VectorSearchResult
                {
                    FilePath = GetFullPath(item.RelativeFilePath),
                    ChunkIndex = item.ChunkIndex,
                    ChunkStartCharacter = item.ChunkStartCharacter,
                    Text = item.Text,
                    Score = Similarity(query, item.Vector)
                })
                .Where(item => normalizedScope == null || string.Equals(item.FilePath, normalizedScope, StringComparison.OrdinalIgnoreCase) || item.FilePath.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Score)
                .Take(topK)
                .ToList();
        }

        public Task SaveAsync(string fileName, CancellationToken cancellationToken)
        {
            return Task.Run(() => Save(fileName, cancellationToken), cancellationToken);
        }

        public static Task<VectorStore> LoadAsync(string fileName, CancellationToken cancellationToken)
        {
            return Task.Run(() => Load(fileName, cancellationToken), cancellationToken);
        }

        private void Save(string fileName, CancellationToken cancellationToken)
        {
            if (Profile.VectorDimension <= 0 && Count > 0)
                throw new InvalidDataException("Vector dimension is not initialized.");

            string directory = Path.GetDirectoryName(fileName) ?? throw new InvalidOperationException("Vector store path has no directory.");
            Directory.CreateDirectory(directory);
            string temporaryFile = fileName + ".tmp";
            string lockFile = fileName + ".lock";
            List<VectorRecord> snapshot;
            lock (syncRoot) snapshot = records.ToList();

            using var storeLock = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(FormatVersion);
                    WriteString(writer, StoreId);
                    WriteString(writer, RootPath);
                    WriteProfile(writer, Profile);
                    writer.Write(CreatedUtc.Ticks);
                    writer.Write(UpdatedUtc.Ticks);
                    writer.Write(snapshot.Count);
                    foreach (VectorRecord record in snapshot)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        WriteString(writer, record.RelativeFilePath);
                        writer.Write(record.FileSize);
                        writer.Write(record.LastWriteTimeUtcTicks);
                        writer.Write(record.ChunkIndex);
                        writer.Write(record.ChunkStartCharacter);
                        WriteString(writer, record.Text);
                        foreach (float value in record.Vector) writer.Write(value);
                    }
                    writer.Flush();
                    stream.Flush(true);
                }
                File.Move(temporaryFile, fileName, true);
            }
            finally
            {
                try { if (File.Exists(temporaryFile)) File.Delete(temporaryFile); } catch { }
            }
        }

        private static VectorStore Load(string fileName, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (!magic.SequenceEqual(Magic)) throw new InvalidDataException("Invalid vector store signature.");
            int version = reader.ReadInt32();
            if (version != FormatVersion) throw new InvalidDataException($"Unsupported vector store version: {version}.");
            string storeId = ReadString(reader);
            string rootPath = ReadString(reader);
            VectorEmbeddingProfile profile = ReadProfile(reader);
            DateTime createdUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            DateTime updatedUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumRecords) throw new InvalidDataException("Invalid vector record count.");
            if (count > 0 && profile.VectorDimension <= 0) throw new InvalidDataException("Invalid vector dimension.");
            long minimumVectorBytes = (long)count * profile.VectorDimension * sizeof(float);
            if (minimumVectorBytes > stream.Length - stream.Position) throw new InvalidDataException("Vector store is truncated or has invalid dimensions.");

            var loaded = new List<VectorRecord>(Math.Min(count, 100_000));
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = ReadString(reader);
                long fileSize = reader.ReadInt64();
                long ticks = reader.ReadInt64();
                int chunkIndex = reader.ReadInt32();
                int chunkStart = reader.ReadInt32();
                string text = ReadString(reader);
                var vector = new float[profile.VectorDimension];
                for (int vectorIndex = 0; vectorIndex < vector.Length; vectorIndex++) vector[vectorIndex] = reader.ReadSingle();
                ValidateVector(vector, profile.VectorDimension);
                loaded.Add(new VectorRecord { RelativeFilePath = relativePath, FileSize = fileSize, LastWriteTimeUtcTicks = ticks, ChunkIndex = chunkIndex, ChunkStartCharacter = chunkStart, Text = text, Vector = vector });
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data at the end of vector store.");
            var store = new VectorStore(storeId, rootPath, profile) { CreatedUtc = createdUtc, UpdatedUtc = updatedUtc, records = loaded };
            store.ValidateRecords(loaded, null);
            return store;
        }

        private void ValidateRecords(IReadOnlyList<VectorRecord> items, string expectedPath)
        {
            foreach (VectorRecord item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.RelativeFilePath)) throw new InvalidDataException("Invalid vector record path.");
                if (expectedPath != null && !PathEquals(item.RelativeFilePath, expectedPath)) throw new InvalidDataException("Vector record belongs to another file.");
                GetFullPath(item.RelativeFilePath);
                ValidateVector(item.Vector, Profile.VectorDimension);
            }
        }

        internal static float[] Normalize(float[] vector)
        {
            double sum = 0;
            foreach (float value in vector) sum += value * value;
            if (sum <= double.Epsilon) throw new InvalidDataException("Embedding vector has zero length.");
            float divisor = (float)Math.Sqrt(sum);
            var result = new float[vector.Length];
            for (int index = 0; index < vector.Length; index++) result[index] = vector[index] / divisor;
            return result;
        }

        internal static void ValidateVector(float[] vector, int expectedDimension)
        {
            if (vector == null || vector.Length == 0 || vector.Length > MaximumVectorDimension || (expectedDimension > 0 && vector.Length != expectedDimension)) throw new InvalidDataException("Embedding vector dimension mismatch.");
            foreach (float value in vector) if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Embedding vector contains a non-finite value.");
        }

        private float Similarity(float[] left, float[] right)
        {
            if (Profile.SimilarityMetric == VectorSimilarityMetric.Cosine)
            {
                float denominator = (float)Math.Sqrt(Dot(left, left)) * (float)Math.Sqrt(Dot(right, right));
                return denominator <= float.Epsilon ? 0 : Dot(left, right) / denominator;
            }
            return Dot(left, right);
        }

        private static float Dot(float[] left, float[] right) { float sum = 0; for (int index = 0; index < left.Length; index++) sum += left[index] * right[index]; return sum; }
        private string GetRelativePath(string filePath) { string fullPath = Path.GetFullPath(filePath); string relative = Path.GetRelativePath(RootPath, fullPath); GetFullPath(relative); return relative; }
        private string GetFullPath(string relativePath) { string fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath)); string prefix = RootPath.EndsWith(Path.DirectorySeparatorChar) ? RootPath : RootPath + Path.DirectorySeparatorChar; if (!string.Equals(fullPath, RootPath, StringComparison.OrdinalIgnoreCase) && !fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Vector record path escapes its root."); return fullPath; }
        private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        private static void WriteString(BinaryWriter writer, string value) { byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); if (bytes.Length > MaximumStringBytes) throw new InvalidDataException("Vector store string is too large."); writer.Write(bytes.Length); writer.Write(bytes); }
        private static string ReadString(BinaryReader reader) { int length = reader.ReadInt32(); if (length < 0 || length > MaximumStringBytes || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid vector store string length."); return Encoding.UTF8.GetString(reader.ReadBytes(length)); }
        private static void WriteProfile(BinaryWriter writer, VectorEmbeddingProfile profile) { WriteString(writer, profile.Provider); WriteString(writer, profile.Endpoint); WriteString(writer, profile.ModelName); WriteString(writer, profile.ModelDigest); writer.Write(profile.VectorDimension); writer.Write((byte)1); writer.Write((byte)profile.SimilarityMetric); writer.Write(profile.VectorsNormalized); writer.Write(profile.ChunkerVersion); writer.Write(profile.ChunkSizeCharacters); writer.Write(profile.ChunkOverlapCharacters); writer.Write(profile.TextExtractorVersion); }
        private static VectorEmbeddingProfile ReadProfile(BinaryReader reader) { var profile = new VectorEmbeddingProfile { Provider = ReadString(reader), Endpoint = ReadString(reader), ModelName = ReadString(reader), ModelDigest = ReadString(reader), VectorDimension = reader.ReadInt32() }; if (reader.ReadByte() != 1) throw new InvalidDataException("Unsupported vector element type."); profile.SimilarityMetric = (VectorSimilarityMetric)reader.ReadByte(); if (!Enum.IsDefined(profile.SimilarityMetric)) throw new InvalidDataException("Invalid similarity metric."); profile.VectorsNormalized = reader.ReadBoolean(); profile.ChunkerVersion = reader.ReadInt32(); profile.ChunkSizeCharacters = reader.ReadInt32(); profile.ChunkOverlapCharacters = reader.ReadInt32(); profile.TextExtractorVersion = reader.ReadInt32(); if (profile.VectorDimension < 0 || profile.VectorDimension > MaximumVectorDimension || profile.ChunkSizeCharacters <= 0 || profile.ChunkOverlapCharacters < 0 || profile.ChunkOverlapCharacters >= profile.ChunkSizeCharacters) throw new InvalidDataException("Invalid vector store profile."); return profile; }
    }
}
