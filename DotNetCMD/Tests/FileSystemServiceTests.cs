using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class FileSystemServiceTests
    {
        [Fact]
        public void EnsureUniqueDirectoryPath_WithoutConflict_ReturnsDefaultName()
        {
            using var directory = new TemporaryDirectory();

            string result = FileSystemService.EnsureUniqueDirectoryPath(directory.Root, "New Folder");

            Assert.Equal(directory.GetPath("New Folder"), result);
        }

        [Fact]
        public void EnsureUniqueDirectoryPath_WithConflict_ReturnsFirstSuffix()
        {
            using var directory = new TemporaryDirectory();
            Directory.CreateDirectory(directory.GetPath("New Folder"));

            string result = FileSystemService.EnsureUniqueDirectoryPath(directory.Root, "New Folder");

            Assert.Equal(directory.GetPath("New Folder (2)"), result);
        }

        [Fact]
        public void EnsureUniqueDirectoryPath_WithSeveralConflicts_ReturnsNextFreeSuffix()
        {
            using var directory = new TemporaryDirectory();
            Directory.CreateDirectory(directory.GetPath("New Folder"));
            Directory.CreateDirectory(directory.GetPath("New Folder (2)"));

            string result = FileSystemService.EnsureUniqueDirectoryPath(directory.Root, "New Folder");

            Assert.Equal(directory.GetPath("New Folder (3)"), result);
        }

        [Fact]
        public void CommitTemporaryFile_MovesTemporaryFileOntoDestination()
        {
            using var directory = new TemporaryDirectory();
            string temporary = directory.GetPath("note.tmp");
            string destination = directory.GetPath("note.txt");
            File.WriteAllText(temporary, "committed");

            FileSystemService.CommitTemporaryFile(temporary, destination, true);

            Assert.False(File.Exists(temporary));
            Assert.Equal("committed", File.ReadAllText(destination));
        }

        [Fact]
        public void CommitTemporaryFile_WithoutOverwrite_KeepsExistingDestination()
        {
            using var directory = new TemporaryDirectory();
            string temporary = directory.GetPath("note.tmp");
            string destination = directory.GetPath("note.txt");
            File.WriteAllText(temporary, "new");
            File.WriteAllText(destination, "old");

            Assert.Throws<IOException>(() => FileSystemService.CommitTemporaryFile(temporary, destination, false));
            Assert.Equal("old", File.ReadAllText(destination));
            Assert.Equal("new", File.ReadAllText(temporary));
        }
    }
}
