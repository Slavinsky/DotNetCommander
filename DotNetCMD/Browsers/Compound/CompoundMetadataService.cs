using OpenMcdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class CompoundMetadataReport
    {
        public CompoundMetadataReport(
            string path,
            string documentType,
            Guid rootClsid,
            IReadOnlyList<CompoundCatalogEntry> entries,
            IReadOnlyList<CompoundPropertySetRow> properties,
            IReadOnlyList<string> protectionEvidence,
            IReadOnlyList<string> encryptionEvidence,
            IReadOnlyList<string> macroEvidence,
            IReadOnlyList<string> embeddedEvidence,
            IReadOnlyList<string> readWarnings)
        {
            Path = path;
            DocumentType = documentType;
            RootClsid = rootClsid;
            Entries = entries ?? Array.Empty<CompoundCatalogEntry>();
            Properties = properties ?? Array.Empty<CompoundPropertySetRow>();
            ProtectionEvidence = protectionEvidence ?? Array.Empty<string>();
            EncryptionEvidence = encryptionEvidence ?? Array.Empty<string>();
            MacroEvidence = macroEvidence ?? Array.Empty<string>();
            EmbeddedEvidence = embeddedEvidence ?? Array.Empty<string>();
            ReadWarnings = readWarnings ?? Array.Empty<string>();
        }

        public string Path { get; }
        public string DocumentType { get; }
        public Guid RootClsid { get; }
        public IReadOnlyList<CompoundCatalogEntry> Entries { get; }
        public IReadOnlyList<CompoundPropertySetRow> Properties { get; }
        public IReadOnlyList<string> ProtectionEvidence { get; }
        public IReadOnlyList<string> EncryptionEvidence { get; }
        public IReadOnlyList<string> MacroEvidence { get; }
        public IReadOnlyList<string> EmbeddedEvidence { get; }
        public IReadOnlyList<string> ReadWarnings { get; }
    }

    internal sealed class CompoundMetadataService
    {
        private const int MaximumPropertyStreamBytes = 8 * 1024 * 1024;
        private const string SummaryInformationName = "\u0005SummaryInformation";
        private const string DocumentSummaryInformationName = "\u0005DocumentSummaryInformation";

        public Task<CompoundMetadataReport> ReadAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => Read(path, cancellationToken), cancellationToken);
        }

        private static CompoundMetadataReport Read(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Compound file was not found.", path);

            using RootStorage root = RootStorage.OpenRead(path, StorageModeFlags.StrictValidation);
            var entries = new List<CompoundCatalogEntry>();
            EnumerateEntries(root, string.Empty, entries, 0, cancellationToken);
            var rootStreamNames = new HashSet<string>(root.EnumerateEntries()
                .Where(entry => entry.Type == EntryType.Stream)
                .Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
            string documentType = CompoundFileCatalogService.ClassifyDocument(rootStreamNames, root.CLSID);

            var properties = new List<CompoundPropertySetRow>();
            var warnings = new List<string>();
            ReadPropertyStream(root, SummaryInformationName, properties, warnings, cancellationToken);
            ReadPropertyStream(root, DocumentSummaryInformationName, properties, warnings, cancellationToken);

            bool hasEncryptionInfo = rootStreamNames.Contains("EncryptionInfo");
            bool hasEncryptedPackage = rootStreamNames.Contains("EncryptedPackage");
            string[] encryptionEvidence = hasEncryptionInfo && hasEncryptedPackage
                ? new[] { "EncryptionInfo", "EncryptedPackage" }
                : Array.Empty<string>();

            string[] macroEvidence = FindMacroEvidence(entries);
            string[] protectionEvidence = FindProtectionEvidence(properties);
            string[] embeddedEvidence = entries
                .Where(entry => !entry.IsStorage && IsEmbeddedPayloadMarker(entry.Name))
                .Select(entry => entry.FullPath)
                .Take(64)
                .ToArray();

            return new CompoundMetadataReport(
                Path.GetFullPath(path),
                documentType,
                root.CLSID,
                entries,
                properties,
                protectionEvidence,
                encryptionEvidence,
                macroEvidence,
                embeddedEvidence,
                warnings);
        }

        private static void ReadPropertyStream(
            Storage root,
            string streamName,
            List<CompoundPropertySetRow> properties,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EntryInfo? matchingInfo = root.EnumerateEntries()
                .Where(entry => entry.Type == EntryType.Stream
                    && string.Equals(entry.Name, streamName, StringComparison.OrdinalIgnoreCase))
                .Select(entry => (EntryInfo?)entry)
                .FirstOrDefault();
            if (!matchingInfo.HasValue)
                return;
            EntryInfo info = matchingInfo.Value;

            try
            {
                using CfbStream stream = root.OpenStream(info.Name);
                if (stream.Length < 0 || stream.Length > MaximumPropertyStreamBytes)
                {
                    warnings.Add(GetDisplayName(info.Name));
                    return;
                }

                byte[] bytes = new byte[checked((int)stream.Length)];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                        throw new EndOfStreamException("The property stream ended before its reported length.");
                    offset += read;
                }

                properties.AddRange(CompoundPropertySetReader.Read(info.Name, bytes));
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogService.LogException("CompoundMetadataService.ReadPropertyStream", ex);
                warnings.Add(GetDisplayName(info.Name));
            }
        }

        private static void EnumerateEntries(
            Storage storage,
            string parentPath,
            List<CompoundCatalogEntry> entries,
            int depth,
            CancellationToken cancellationToken)
        {
            if (depth > 256)
                throw new InvalidDataException("Compound storage nesting is too deep.");

            foreach (EntryInfo info in storage.EnumerateEntries())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= 200000)
                    throw new InvalidDataException("Compound file contains too many directory entries.");

                string fullPath = string.IsNullOrEmpty(parentPath) ? info.Name : parentPath + "/" + info.Name;
                bool isStorage = info.Type == EntryType.Storage;
                entries.Add(new CompoundCatalogEntry
                {
                    Name = info.Name,
                    FullPath = fullPath,
                    ParentPath = parentPath,
                    IsStorage = isStorage,
                    Size = isStorage ? null : info.Length,
                    Created = info.CreationTime,
                    Modified = info.ModifiedTime,
                    Clsid = info.CLSID
                });

                if (isStorage)
                    EnumerateEntries(storage.OpenStorage(info.Name), fullPath, entries, depth + 1, cancellationToken);
            }
        }

        private static string[] FindMacroEvidence(IReadOnlyList<CompoundCatalogEntry> entries)
        {
            bool hasVbaStorage = entries.Any(entry => entry.IsStorage
                && string.Equals(entry.Name, "VBA", StringComparison.OrdinalIgnoreCase));
            if (!hasVbaStorage)
                return Array.Empty<string>();

            return entries
                .Where(entry => !entry.IsStorage
                    && (entry.FullPath.StartsWith("VBA/", StringComparison.OrdinalIgnoreCase)
                        || entry.FullPath.IndexOf("/VBA/", StringComparison.OrdinalIgnoreCase) >= 0)
                    && (string.Equals(entry.Name, "dir", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.Name, "_VBA_PROJECT", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.Name, "VBA_PROJECT", StringComparison.OrdinalIgnoreCase)))
                .Select(entry => entry.FullPath)
                .Take(64)
                .ToArray();
        }

        private static string[] FindProtectionEvidence(IReadOnlyList<CompoundPropertySetRow> properties)
        {
            CompoundPropertySetRow security = properties.FirstOrDefault(property =>
                property.PropertyId == 19
                && property.TypeName == "I4"
                && property.Name.StartsWith("Summary Information", StringComparison.Ordinal));
            if (security == null || !int.TryParse(security.Value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int flags))
                return Array.Empty<string>();

            var evidence = new List<string>();
            AddProtectionFlag(evidence, flags, 0x1, "compoundMetadataSecurityPassword");
            AddProtectionFlag(evidence, flags, 0x2, "compoundMetadataSecurityReadOnlyRecommended");
            AddProtectionFlag(evidence, flags, 0x4, "compoundMetadataSecurityReadOnlyEnforced");
            AddProtectionFlag(evidence, flags, 0x8, "compoundMetadataSecurityAnnotationsLocked");
            int unknownFlags = flags & ~0xF;
            if (unknownFlags != 0)
                evidence.Add("SummaryInformation PID 19: 0x" + unknownFlags.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            return evidence.ToArray();
        }

        private static void AddProtectionFlag(List<string> evidence, int flags, int mask, string resourceKey)
        {
            if ((flags & mask) != 0)
                evidence.Add("SummaryInformation PID 19: " + Language.getString(resourceKey));
        }

        private static bool IsEmbeddedPayloadMarker(string name)
        {
            return CompoundFileCatalogService.IsOle10NativeStreamName(name)
                || string.Equals(name, "Package", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDisplayName(string name)
        {
            return !string.IsNullOrEmpty(name) && char.IsControl(name[0])
                ? "\\u" + ((int)name[0]).ToString("X4") + name.Substring(1)
                : name;
        }
    }
}
