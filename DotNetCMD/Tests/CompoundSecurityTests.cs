using System;
using System.IO;
using System.Linq;
using System.Threading;
using DotNetCommander;
using OpenMcdf;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class CompoundSecurityTests
    {
        [Fact]
        public async System.Threading.Tasks.Task ReadCatalog_ObjectPoolDescendants_AreMarkedEmbedded()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("doc.doc");
            CreateCompoundFile(path);

            using var service = new CompoundFileCatalogService();
            CompoundFileCatalog catalog = await service.ReadCatalogAsync(path, CancellationToken.None);

            CompoundCatalogEntry word = catalog.Entries.Single(entry => entry.FullPath == "WordDocument");
            CompoundCatalogEntry pool = catalog.Entries.Single(entry => entry.FullPath == "ObjectPool");
            CompoundCatalogEntry objectStorage = catalog.Entries.Single(entry => entry.FullPath == "ObjectPool/_1234");
            CompoundCatalogEntry payload = catalog.Entries.Single(entry => entry.FullPath == "ObjectPool/_1234/CONTENTS");

            Assert.False(word.IsEmbedded);
            Assert.False(pool.IsEmbedded);
            Assert.True(objectStorage.IsEmbedded);
            Assert.True(payload.IsEmbedded);
        }

        [Fact]
        public void CleanupStaleMaterializationRoots_RemovesSessionChildren_KeepsBaseAndSiblings()
        {
            using var directory = new TemporaryDirectory();
            string baseDirectory = directory.GetPath("CompoundPreview");
            string firstSession = Path.Combine(baseDirectory, "aaaa");
            string secondSession = Path.Combine(baseDirectory, "bbbb");
            Directory.CreateDirectory(firstSession);
            Directory.CreateDirectory(secondSession);
            File.WriteAllText(Path.Combine(firstSession, "cached.bin"), "data");
            File.WriteAllText(Path.Combine(baseDirectory, "loose.tmp"), "data");
            string sibling = directory.GetPath("outside");
            Directory.CreateDirectory(sibling);
            File.WriteAllText(Path.Combine(sibling, "keep.txt"), "data");

            CompoundFileCatalogService.CleanupStaleMaterializationRoots(baseDirectory);

            Assert.True(Directory.Exists(baseDirectory));
            Assert.Empty(Directory.GetFileSystemEntries(baseDirectory));
            Assert.True(File.Exists(Path.Combine(sibling, "keep.txt")));
        }

        [Fact]
        public void CleanupStaleMaterializationRoots_MissingDirectory_DoesNotThrow()
        {
            using var directory = new TemporaryDirectory();

            CompoundFileCatalogService.CleanupStaleMaterializationRoots(directory.GetPath("does-not-exist"));
            CompoundFileCatalogService.CleanupStaleMaterializationRoots(null);
            CompoundFileCatalogService.CleanupStaleMaterializationRoots(string.Empty);
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
