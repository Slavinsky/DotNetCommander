using OpenMcdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class CompoundFileCatalogService : IDisposable
    {
        private const int MaximumEntries = 200000;
        private const int MaximumDepth = 256;
        private readonly string materializationRoot = Path.Combine(
            Path.GetTempPath(),
            "DotNetCommander",
            "CompoundPreview",
            Guid.NewGuid().ToString("N"));

        public Task<CompoundFileCatalog> ReadCatalogAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => ReadCatalog(path, cancellationToken), cancellationToken);
        }

        public Task<string> MaterializeStreamAsync(
            string compoundPath,
            string streamPath,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => MaterializeStream(compoundPath, streamPath, cancellationToken), cancellationToken);
        }

        public void Dispose()
        {
            TryDeleteMaterializationRoot();
        }

        private static CompoundFileCatalog ReadCatalog(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Compound file was not found.", path);

            using RootStorage root = RootStorage.OpenRead(path, StorageModeFlags.StrictValidation);
            var entries = new List<CompoundCatalogEntry>();
            var streamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnumerateStorage(root, string.Empty, 0, entries, streamNames, false, cancellationToken);
            return new CompoundFileCatalog(
                Path.GetFullPath(path),
                ClassifyDocument(streamNames, root.CLSID),
                root.CLSID,
                entries);
        }

        internal static void CleanupStaleMaterializationRoots(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
                return;

            foreach (string child in Directory.EnumerateFileSystemEntries(baseDirectory))
            {
                try
                {
                    if (Directory.Exists(child))
                        Directory.Delete(child, true);
                    else
                        File.Delete(child);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    LogService.LogException("CompoundFileCatalogService.CleanupStaleMaterializationRoots", ex);
                }
            }
        }

        private static void EnumerateStorage(
            Storage storage,
            string parentPath,
            int depth,
            List<CompoundCatalogEntry> entries,
            HashSet<string> streamNames,
            bool untrustedInherited,
            CancellationToken cancellationToken)
        {
            if (depth > MaximumDepth)
                throw new InvalidDataException("Compound storage nesting is too deep.");

            foreach (EntryInfo info in storage.EnumerateEntries())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= MaximumEntries)
                    throw new InvalidDataException("Compound file contains too many directory entries.");

                string fullPath = CombineInternalPath(parentPath, info.Name);
                bool isStorage = info.Type == EntryType.Storage;
                bool isObjectPool = string.Equals(info.Name, "ObjectPool", StringComparison.OrdinalIgnoreCase);
                entries.Add(new CompoundCatalogEntry
                {
                    Name = info.Name,
                    FullPath = fullPath,
                    ParentPath = parentPath,
                    IsStorage = isStorage,
                    Size = isStorage ? null : info.Length,
                    Created = NormalizeDate(info.CreationTime),
                    Modified = NormalizeDate(info.ModifiedTime),
                    Clsid = info.CLSID,
                    IsEmbedded = untrustedInherited || info.CLSID != Guid.Empty
                });

                if (isStorage)
                {
                    bool childUntrusted = untrustedInherited || info.CLSID != Guid.Empty || isObjectPool;
                    Storage child = storage.OpenStorage(info.Name);
                    EnumerateStorage(child, fullPath, depth + 1, entries, streamNames, childUntrusted, cancellationToken);
                }
                else
                {
                    streamNames.Add(info.Name);
                }
            }
        }

        private string MaterializeStream(string compoundPath, string streamPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(streamPath))
                return null;

            string[] segments = streamPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            using RootStorage root = RootStorage.OpenRead(compoundPath, StorageModeFlags.StrictValidation);
            Storage storage = root;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                storage = storage.OpenStorage(segments[index]);
            }

            using CfbStream source = storage.OpenStream(segments[segments.Length - 1]);
            Directory.CreateDirectory(materializationRoot);
            string safeName = MakeSafeFileName(segments[segments.Length - 1]);
            string cacheKey = Path.GetFullPath(compoundPath) + "|" + File.GetLastWriteTimeUtc(compoundPath).Ticks + "|" + streamPath;
            string targetPath = Path.Combine(materializationRoot, BuildPathHash(cacheKey) + "_" + safeName);
            if (File.Exists(targetPath))
                return targetPath;
            using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            byte[] buffer = new byte[81920];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
                output.Write(buffer, 0, read);
            }

            return targetPath;
        }

        private static string ClassifyDocument(ISet<string> streams, Guid rootClsid)
        {
            if (streams.Contains("WordDocument"))
                return "Microsoft Word Document";
            if (streams.Contains("Workbook") || streams.Contains("Book"))
                return "Microsoft Excel Workbook";
            if (streams.Contains("PowerPoint Document"))
                return "Microsoft PowerPoint Presentation";
            if (streams.Contains("__properties_version1.0"))
                return "Microsoft Outlook Message";
            if (streams.Contains("_Tables") && streams.Contains("_StringData"))
                return "Windows Installer Database";
            if (streams.Contains("VisioDocument"))
                return "Microsoft Visio Drawing";
            if (streams.Contains("MSProject"))
                return "Microsoft Project Document";
            if (streams.Contains("Catalog"))
                return "Windows Thumbnail Cache";

            string clsidType = ClassifyClsid(rootClsid);
            return clsidType ?? "Compound File Binary Format";
        }

        private static string ClassifyClsid(Guid clsid)
        {
            if (clsid == new Guid("00020906-0000-0000-C000-000000000046"))
                return "Microsoft Word Document";
            if (clsid == new Guid("00020820-0000-0000-C000-000000000046"))
                return "Microsoft Excel Workbook";
            if (clsid == new Guid("64818D10-4F9B-11CF-86EA-00AA00B929E8"))
                return "Microsoft PowerPoint Presentation";
            if (clsid == new Guid("00021A14-0000-0000-C000-000000000046"))
                return "Microsoft Visio Drawing";
            if (clsid == new Guid("000C1084-0000-0000-C000-000000000046"))
                return "Windows Installer Database";
            return null;
        }

        private static DateTime? NormalizeDate(DateTime value)
        {
            return value == DateTime.MinValue || value == DateTime.MaxValue ? null : value.ToLocalTime();
        }

        private static string CombineInternalPath(string parent, string name)
        {
            return string.IsNullOrEmpty(parent) ? name : parent + "/" + name;
        }

        internal static string MakeSafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(Math.Max(1, name?.Length ?? 0));
            foreach (char character in name ?? string.Empty)
            {
                builder.Append(char.IsControl(character) || invalid.Contains(character) ? '_' : character);
            }

            string result = builder.ToString().Trim().TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(result) ? "stream.bin" : result;
        }

        private static string BuildPathHash(string path)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
            return Convert.ToHexString(hash, 0, 6);
        }

        private void TryDeleteMaterializationRoot()
        {
            try
            {
                string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DotNetCommander", "CompoundPreview"));
                string resolved = Path.GetFullPath(materializationRoot);
                if (resolved.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(resolved))
                {
                    Directory.Delete(resolved, true);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LogService.LogException("CompoundFileCatalogService.Dispose", ex);
            }
        }
    }

    internal sealed class CompoundFileCatalog
    {
        public CompoundFileCatalog(string path, string documentType, Guid rootClsid, IReadOnlyList<CompoundCatalogEntry> entries)
        {
            Path = path;
            DocumentType = documentType;
            RootClsid = rootClsid;
            Entries = entries ?? Array.Empty<CompoundCatalogEntry>();
        }

        public string Path { get; }
        public string DocumentType { get; }
        public Guid RootClsid { get; }
        public IReadOnlyList<CompoundCatalogEntry> Entries { get; }
    }

    internal sealed class CompoundCatalogEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string ParentPath { get; set; }
        public bool IsStorage { get; set; }
        public bool IsEmbedded { get; set; }
        public long? Size { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }
        public Guid Clsid { get; set; }
    }
}
