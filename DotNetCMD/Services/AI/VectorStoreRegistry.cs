using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class VectorStoreDescriptor
    {
        public string StoreId { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    internal sealed class VectorStoreScopeConflictException : InvalidOperationException
    {
        public VectorStoreScopeConflictException(string message) : base(message) { }
    }

    internal sealed class VectorStoreRegistry
    {
        private const string RegistryFileName = "stores.json";
        private readonly string basePath;
        private readonly List<VectorStoreDescriptor> stores = new List<VectorStoreDescriptor>();

        public VectorStoreRegistry(string basePath)
        {
            this.basePath = Path.GetFullPath(basePath ?? throw new ArgumentNullException(nameof(basePath)));
        }

        public IReadOnlyList<VectorStoreDescriptor> Stores => stores;
        public string GetStoreFilePath(VectorStoreDescriptor descriptor) => Path.Combine(basePath, descriptor.StoreId, "vectors.dat");

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            stores.Clear();
            string fileName = Path.Combine(basePath, RegistryFileName);
            if (!File.Exists(fileName)) return;
            await using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, true);
            List<VectorStoreDescriptor> loaded = await JsonSerializer.DeserializeAsync<List<VectorStoreDescriptor>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new List<VectorStoreDescriptor>();
            foreach (VectorStoreDescriptor descriptor in loaded)
            {
                descriptor.RootPath = NormalizeRoot(descriptor.RootPath);
                if (string.IsNullOrWhiteSpace(descriptor.StoreId) || stores.Any(item => string.Equals(item.StoreId, descriptor.StoreId, StringComparison.OrdinalIgnoreCase) || PathEquals(item.RootPath, descriptor.RootPath)))
                    throw new InvalidDataException("Invalid or duplicate vector store registry entry.");
                stores.Add(descriptor);
            }
        }

        public VectorStoreDescriptor ResolveExisting(string rootPath)
        {
            string normalized = NormalizeRoot(rootPath);
            return stores.Where(item => IsSameOrParent(item.RootPath, normalized)).OrderByDescending(item => item.RootPath.Length).FirstOrDefault();
        }

        public VectorStoreDescriptor PrepareNew(string rootPath)
        {
            string normalized = NormalizeRoot(rootPath);
            VectorStoreDescriptor existing = ResolveExisting(normalized);
            if (existing != null) return existing;
            List<VectorStoreDescriptor> descendants = stores.Where(item => IsSameOrParent(normalized, item.RootPath)).ToList();
            if (descendants.Count > 0) throw new VectorStoreScopeConflictException("The selected folder contains an existing vector store scope. Merge or replace it explicitly.");
            return new VectorStoreDescriptor { StoreId = CreateStoreId(normalized), RootPath = normalized, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
        }

        public async Task PublishAsync(VectorStoreDescriptor descriptor, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(basePath);
            string fileName = Path.Combine(basePath, RegistryFileName);
            string temporaryFile = fileName + ".tmp";
            string lockFile = fileName + ".lock";
            using var registryLock = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(fileName))
            {
                await using var input = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, true);
                List<VectorStoreDescriptor> latest = await JsonSerializer.DeserializeAsync<List<VectorStoreDescriptor>>(input, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new List<VectorStoreDescriptor>();
                stores.Clear();
                stores.AddRange(latest);
            }
            VectorStoreDescriptor existing = stores.FirstOrDefault(item => string.Equals(item.StoreId, descriptor.StoreId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                if (stores.Any(item => IsSameOrParent(item.RootPath, descriptor.RootPath) || IsSameOrParent(descriptor.RootPath, item.RootPath)))
                    throw new VectorStoreScopeConflictException("Another process registered an overlapping vector store scope.");
                stores.Add(descriptor);
            }
            else
            {
                existing.UpdatedUtc = descriptor.UpdatedUtc;
            }
            try
            {
                await using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, true))
                {
                    await JsonSerializer.SerializeAsync(stream, stores, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(true);
                }
                File.Move(temporaryFile, fileName, true);
            }
            finally
            {
                try { if (File.Exists(temporaryFile)) File.Delete(temporaryFile); } catch { }
            }
        }

        internal static string NormalizeRoot(string path)
        {
            string fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) ? fullPath : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string CreateStoreId(string rootPath)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rootPath.ToUpperInvariant()));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool IsSameOrParent(string candidateParent, string path)
        {
            if (PathEquals(candidateParent, path)) return true;
            string prefix = candidateParent.EndsWith(Path.DirectorySeparatorChar) ? candidateParent : candidateParent + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
