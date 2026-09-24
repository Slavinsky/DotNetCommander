using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetCommander;
using OpenMcdf;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class CompoundParsingTests
    {
        [Fact]
        public async Task ReadCatalog_MissingFile_ThrowsFileNotFoundException()
        {
            using var directory = new TemporaryDirectory();
            using var service = new CompoundFileCatalogService();

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.ReadCatalogAsync(directory.GetPath("missing.doc"), CancellationToken.None));
        }

        [Fact]
        public async Task ReadCatalog_GarbageBytes_ThrowsFileFormatException()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("garbage.doc");
            File.WriteAllText(path, "this is not a compound file at all, just ascii padding padding");

            using var service = new CompoundFileCatalogService();
            await Assert.ThrowsAsync<OpenMcdf.FileFormatException>(
                () => service.ReadCatalogAsync(path, CancellationToken.None));
        }

        [Fact]
        public async Task ReadCatalog_TruncatedFile_ThrowsEndOfStreamException()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source.doc");
            CreateCompoundFile(source);
            byte[] full = File.ReadAllBytes(source);
            string truncated = directory.GetPath("truncated.doc");
            File.WriteAllBytes(truncated, full.AsSpan(0, Math.Min(full.Length, 128)).ToArray());

            using var service = new CompoundFileCatalogService();
            await Assert.ThrowsAsync<EndOfStreamException>(
                () => service.ReadCatalogAsync(truncated, CancellationToken.None));
        }

        [Fact]
        public async Task ReadCatalog_NestedEntries_ExposeFullPathParentPathAndSizes()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            Assert.Equal(Path.GetFullPath(path), catalog.Path);
            Assert.Equal("Microsoft Word Document", catalog.DocumentType);

            CompoundCatalogEntry word = catalog.Entries.Single(entry => entry.FullPath == "WordDocument");
            Assert.Equal(string.Empty, word.ParentPath);
            Assert.False(word.IsStorage);
            Assert.Equal(3, word.Size);

            CompoundCatalogEntry pool = catalog.Entries.Single(entry => entry.FullPath == "ObjectPool");
            Assert.True(pool.IsStorage);
            Assert.Null(pool.Size);

            CompoundCatalogEntry inner = catalog.Entries.Single(entry => entry.FullPath == "ObjectPool/_1234/CONTENTS");
            Assert.Equal("ObjectPool/_1234", inner.ParentPath);
            Assert.Equal("CONTENTS", inner.Name);
            Assert.False(inner.IsStorage);
            Assert.Equal(2, inner.Size);
        }

        public static TheoryData<string[], string> ClassificationCases()
        {
            return new TheoryData<string[], string>
            {
                { new[] { "WordDocument" }, "Microsoft Word Document" },
                { new[] { "Workbook" }, "Microsoft Excel Workbook" },
                { new[] { "Book" }, "Microsoft Excel Workbook" },
                { new[] { "PowerPoint Document" }, "Microsoft PowerPoint Presentation" },
                { new[] { "__properties_version1.0" }, "Microsoft Outlook Message" },
                { new[] { "_Tables", "_StringData" }, "Windows Installer Database" },
                { new[] { "VisioDocument" }, "Microsoft Visio Drawing" },
                { new[] { "MSProject" }, "Microsoft Project Document" },
                { new[] { "Catalog" }, "Windows Thumbnail Cache" },
                { new[] { "SomethingElse" }, "Compound File Binary Format" }
            };
        }

        [Theory]
        [MemberData(nameof(ClassificationCases))]
        public async Task ReadCatalog_ClassifiesByStreamSignatures(string[] streamNames, string expectedType)
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                foreach (string streamName in streamNames)
                {
                    using CfbStream stream = root.CreateStream(streamName);
                    byte[] data = { 0x41 };
                    stream.Write(data, 0, data.Length);
                }

                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            Assert.Equal(expectedType, catalog.DocumentType);
        }

        [Fact]
        public async Task ReadCatalog_ClassifiesByRootClsid_WhenNoSignatureStreams()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                using CfbStream stream = root.CreateStream("Payload");
                byte[] data = { 0x42 };
                stream.Write(data, 0, data.Length);
                root.CLSID = new Guid("00020820-0000-0000-C000-000000000046");
                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            Assert.Equal("Microsoft Excel Workbook", catalog.DocumentType);
            Assert.Equal(new Guid("00020820-0000-0000-C000-000000000046"), catalog.RootClsid);
        }

        [Fact]
        public async Task ReadCatalog_UnknownRootClsid_FallsBackToGenericTypeName()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                using CfbStream stream = root.CreateStream("Payload");
                byte[] data = { 0x43 };
                stream.Write(data, 0, data.Length);
                root.CLSID = Guid.NewGuid();
                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            Assert.Equal("Compound File Binary Format", catalog.DocumentType);
        }

        [Fact]
        public async Task ReadCatalog_StorageWithClsid_MarksItAndDescendantsEmbedded()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                Storage tagged = root.CreateStorage("Tagged");
                tagged.CLSID = Guid.NewGuid();
                using (CfbStream payload = tagged.CreateStream("Payload"))
                {
                    byte[] data = { 0x44 };
                    payload.Write(data, 0, data.Length);
                }

                Storage plain = root.CreateStorage("Plain");
                using (CfbStream payload = plain.CreateStream("Payload"))
                {
                    byte[] data = { 0x45 };
                    payload.Write(data, 0, data.Length);
                }

                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            Assert.True(catalog.Entries.Single(entry => entry.FullPath == "Tagged").IsEmbedded);
            Assert.True(catalog.Entries.Single(entry => entry.FullPath == "Tagged/Payload").IsEmbedded);
            Assert.False(catalog.Entries.Single(entry => entry.FullPath == "Plain").IsEmbedded);
            Assert.False(catalog.Entries.Single(entry => entry.FullPath == "Plain/Payload").IsEmbedded);
        }

        [Fact]
        public async Task ReadCatalog_DeepNesting_ThrowsInvalidDataException()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("deep.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                Storage current = root;
                for (int level = 0; level < 260; level++)
                {
                    current = current.CreateStorage("s" + level);
                }

                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => service.ReadCatalogAsync(path, CancellationToken.None));
        }

        [Fact]
        public async Task ReadCatalog_PreCancelledToken_ThrowsOperationCanceled()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.ReadCatalogAsync(path, cancellation.Token));
        }

        [Theory]
        [InlineData("report.txt", "report.txt")]
        [InlineData("a<b>c:d\"e/f\\g|h?i*j", "a_b_c_d_e_f_g_h_i_j")]
        [InlineData("ctrl\u0001chars", "ctrl_chars")]
        [InlineData("name..", "name")]
        [InlineData("name   ", "name")]
        [InlineData("name. . .", "name")]
        [InlineData("..", "stream.bin")]
        [InlineData("...", "stream.bin")]
        [InlineData("", "stream.bin")]
        [InlineData("   ", "stream.bin")]
        [InlineData("con", "con")]
        [InlineData(null, "stream.bin")]
        public void MakeSafeFileName_SanitizesNamesForMaterializedFiles(string? input, string expected)
        {
            Assert.Equal(expected, CompoundFileCatalogService.MakeSafeFileName(input));
        }

        [Fact]
        public async Task MaterializeStream_StreamNamedDotDot_StaysInsideMaterializationRoot()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                using (CfbStream stream = root.CreateStream(".."))
                {
                    byte[] data = { 0x01, 0x02, 0x03 };
                    stream.Write(data, 0, data.Length);
                }

                root.Commit();
            }

            string target;
            using (var service = new CompoundFileCatalogService())
            {
                target = await service.MaterializeStreamAsync(path, "..", CancellationToken.None);

                Assert.NotNull(target);
                string sessionRoot = Path.GetDirectoryName(target);
                Assert.StartsWith(
                    Path.Combine(Path.GetTempPath(), "DotNetCommander", "CompoundPreview") + Path.DirectorySeparatorChar,
                    sessionRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith("stream.bin", target);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, File.ReadAllBytes(target));
            }

            Assert.False(Directory.Exists(Path.GetDirectoryName(target)));
        }

        [Fact]
        public async Task MaterializeStream_SameLeafNameInDifferentStorages_UsesDistinctCacheFiles()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            using (RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted))
            {
                foreach ((string storageName, byte marker) in new[] { ("alpha", (byte)0xA1), ("beta", (byte)0xB2) })
                {
                    Storage storage = root.CreateStorage(storageName);
                    using CfbStream stream = storage.CreateStream("data.bin");
                    byte[] data = { marker, marker };
                    stream.Write(data, 0, data.Length);
                }

                root.Commit();
            }

            using var service = new CompoundFileCatalogService();
            string first = await service.MaterializeStreamAsync(path, "alpha/data.bin", CancellationToken.None);
            string second = await service.MaterializeStreamAsync(path, "beta/data.bin", CancellationToken.None);

            Assert.NotEqual(first, second);
            Assert.Equal(new byte[] { 0xA1, 0xA1 }, File.ReadAllBytes(first));
            Assert.Equal(new byte[] { 0xB2, 0xB2 }, File.ReadAllBytes(second));
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData(null)]
        public async Task MaterializeStream_EmptyStreamPath_ReturnsNull(string? streamPath)
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            Assert.Null(await service.MaterializeStreamAsync(path, streamPath, CancellationToken.None));
        }

        [Fact]
        public async Task MaterializeStream_MissingStream_Throws()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            await Assert.ThrowsAnyAsync<Exception>(
                () => service.MaterializeStreamAsync(path, "NoSuchStream", CancellationToken.None));
        }

        [Fact]
        public async Task MaterializeStream_MissingStorageSegment_Throws()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            await Assert.ThrowsAnyAsync<Exception>(
                () => service.MaterializeStreamAsync(path, "NoSuchStorage/Payload", CancellationToken.None));
        }

        private static void CreateCompoundFile(string path)
        {
            using RootStorage root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted);
            using (CfbStream word = root.CreateStream("WordDocument"))
            {
                byte[] data = { 0x41, 0x42, 0x43 };
                word.Write(data, 0, data.Length);
            }

            Storage pool = root.CreateStorage("ObjectPool");
            Storage objectStorage = pool.CreateStorage("_1234");
            using (CfbStream payload = objectStorage.CreateStream("CONTENTS"))
            {
                byte[] data = { 0x4D, 0x5A };
                payload.Write(data, 0, data.Length);
            }

            root.Commit();
        }
    }
}
