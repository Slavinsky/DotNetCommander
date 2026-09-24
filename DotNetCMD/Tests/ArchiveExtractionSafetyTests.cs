using System.IO.Compression;
using System.Text;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class ArchiveExtractionSafetyTests
    {
        [Fact]
        public async Task ExtractArchiveAsync_ZipWithParentTraversalEntry_ThrowsAndWritesNothingOutside()
        {
            using var directory = new TemporaryDirectory();
            string archivePath = directory.GetPath("evil.zip");
            CreateZipWithEntries(archivePath, ("../escape.txt", "escaped"));

            string destination = directory.GetPath("out");
            string outsidePath = System.IO.Path.Combine(directory.Root, "escape.txt");

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ArchiveService.ExtractArchiveAsync(archivePath, destination, true, null, CancellationToken.None));

            Assert.False(File.Exists(outsidePath));
            Assert.False(File.Exists(System.IO.Path.Combine(destination, "escape.txt")));
        }

        [Fact]
        public async Task ExtractArchiveAsync_ZipWithBackslashTraversalEntry_Throws()
        {
            using var directory = new TemporaryDirectory();
            string archivePath = directory.GetPath("evil.zip");
            CreateZipWithEntries(archivePath, ("sub\\..\\..\\escape.txt", "escaped"));

            string destination = directory.GetPath("out");
            string outsidePath = System.IO.Path.Combine(directory.Root, "escape.txt");

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ArchiveService.ExtractArchiveAsync(archivePath, destination, true, null, CancellationToken.None));

            Assert.False(File.Exists(outsidePath));
        }

        [Fact]
        public async Task ExtractArchiveAsync_ValidZip_ExtractsEntriesInsideDestination()
        {
            using var directory = new TemporaryDirectory();
            string archivePath = directory.GetPath("sample.zip");
            CreateZipWithEntries(archivePath, ("folder/note.txt", "hello"));

            string destination = directory.GetPath("out");
            ArchiveOperationResult result = await ArchiveService.ExtractArchiveAsync(
                archivePath,
                destination,
                true,
                null,
                CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal(1, result.CompletedEntries);
            Assert.Equal(
                "hello",
                File.ReadAllText(System.IO.Path.Combine(destination, "folder", "note.txt")));
        }

        [Fact]
        public async Task ExtractArchiveAsync_WithoutOverwrite_KeepsExistingDestinationFile()
        {
            using var directory = new TemporaryDirectory();
            string archivePath = directory.GetPath("sample.zip");
            CreateZipWithEntries(archivePath, ("note.txt", "from-archive"));

            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existing = System.IO.Path.Combine(destination, "note.txt");
            File.WriteAllText(existing, "original");

            ArchiveOperationResult result = await ArchiveService.ExtractArchiveAsync(
                archivePath,
                destination,
                false,
                null,
                CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal(1, result.SkippedEntries);
            Assert.Equal("original", File.ReadAllText(existing));
        }

        private static void CreateZipWithEntries(string path, params (string Name, string Content)[] entries)
        {
            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using Stream stream = entry.Open();
                byte[] payload = Encoding.UTF8.GetBytes(content);
                stream.Write(payload, 0, payload.Length);
            }
        }
    }
}
