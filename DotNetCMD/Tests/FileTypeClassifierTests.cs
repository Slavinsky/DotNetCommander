using System.IO.Compression;
using System.Formats.Tar;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class FileTypeClassifierTests
    {
        [Theory]
        [InlineData("archive.zip", "Zip")]
        [InlineData("ARCHIVE.ZIP", "Zip")]
        [InlineData("backup.tar", "Tar")]
        [InlineData("backup.tar.gz", "TarGZip")]
        [InlineData("backup.tgz", "TarGZip")]
        public void TryGetArchiveFormatByExtension_KnownExtensions_ReturnsFormat(string path, string expectedFormat)
        {
            bool detected = FileTypeClassifier.TryGetArchiveFormatByExtension(path, out ArchiveFormat format);
            Assert.True(detected);
            Assert.Equal(expectedFormat, format.ToString());
        }

        [Theory]
        [InlineData("archive.7z")]
        [InlineData("archive.rar")]
        [InlineData("plain.txt")]
        [InlineData("")]
        public void TryGetArchiveFormatByExtension_UnsupportedExtensions_ReturnsFalse(string path)
        {
            Assert.False(FileTypeClassifier.TryGetArchiveFormatByExtension(path, out _));
        }

        [Theory]
        [InlineData(".txt", "Text")]
        [InlineData(".csv", "Csv")]
        [InlineData(".md", "Markdown")]
        [InlineData(".rtf", "RichText")]
        [InlineData(".png", "Image")]
        [InlineData(".zip", "Archive")]
        [InlineData(".unknownext", "Binary")]
        public void ClassifyExtension_ReturnsExpectedKind(string extension, string expectedKind)
        {
            FileContentKind kind = FileTypeClassifier.ClassifyExtension(extension);
            Assert.Equal(expectedKind, kind.ToString());
        }

        [Fact]
        public void TryDetectArchiveFormat_RealZipFile_ReturnsZip()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("sample.zip");
            CreateSampleZip(path, "readme.txt", "hello");

            bool detected = FileTypeClassifier.TryDetectArchiveFormat(path, out ArchiveFormat format);

            Assert.True(detected);
            Assert.Equal(ArchiveFormat.Zip, format);
        }

        [Fact]
        public void TryDetectArchiveFormat_RealTarFile_ReturnsTar()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            Directory.CreateDirectory(source);
            File.WriteAllText(directory.GetPath("source/readme.txt"), "hello");
            string path = directory.GetPath("sample.tar");
            TarFile.CreateFromDirectory(source, path, includeBaseDirectory: false);

            bool detected = FileTypeClassifier.TryDetectArchiveFormat(path, out ArchiveFormat format);

            Assert.True(detected);
            Assert.Equal(ArchiveFormat.Tar, format);
        }

        [Fact]
        public void TryDetectArchiveFormat_RealTarGZipFile_ReturnsTarGZip()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            Directory.CreateDirectory(source);
            File.WriteAllText(directory.GetPath("source/readme.txt"), "hello");
            string tarPath = directory.GetPath("sample.tar");
            TarFile.CreateFromDirectory(source, tarPath, includeBaseDirectory: false);

            string path = directory.GetPath("sample.tar.gz");
            using (var input = File.OpenRead(tarPath))
            using (var output = File.Create(path))
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                input.CopyTo(gzip);
            }

            bool detected = FileTypeClassifier.TryDetectArchiveFormat(path, out ArchiveFormat format);

            Assert.True(detected);
            Assert.Equal(ArchiveFormat.TarGZip, format);
        }

        [Fact]
        public void TryDetectArchiveFormat_PlainTextFile_ReturnsFalse()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("notes.txt");
            File.WriteAllText(path, "just some plain text without any container signature");

            Assert.False(FileTypeClassifier.TryDetectArchiveFormat(path, out _));
        }

        [Fact]
        public void IsCompoundFile_WithCfbfSignature_ReturnsTrue()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("legacy.doc");
            byte[] signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00 };
            File.WriteAllBytes(path, signature);

            Assert.True(FileTypeClassifier.IsCompoundFile(path));
        }

        [Fact]
        public void IsCompoundFile_WithPlainText_ReturnsFalse()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("plain.txt");
            File.WriteAllText(path, "not a compound file");

            Assert.False(FileTypeClassifier.IsCompoundFile(path));
        }

        [Fact]
        public void Classify_Directory_ReturnsDirectory()
        {
            using var directory = new TemporaryDirectory();
            Assert.Equal(FileContentKind.Directory, FileTypeClassifier.Classify(directory.Root));
        }

        [Fact]
        public void Classify_RealZipFile_ReturnsArchive()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("sample.zip");
            CreateSampleZip(path, "readme.txt", "hello");

            Assert.Equal(FileContentKind.Archive, FileTypeClassifier.Classify(path));
        }

        [Fact]
        public void Classify_UnknownExtensionWithXmlDeclaration_ReturnsText()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("book.fb2");
            File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?><FictionBook />");

            Assert.Equal(FileContentKind.Text, FileTypeClassifier.Classify(path));
        }

        [Fact]
        public void Classify_UnknownExtensionWithPlainTextPrefix_ReturnsText()
        {
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("data.unknownext");
            File.WriteAllText(path, "plain text payload without any binary control characters");

            Assert.Equal(FileContentKind.Text, FileTypeClassifier.Classify(path));
        }

        [Fact]
        public void GetComparisonMode_ForTwoTextFiles_ReturnsText()
        {
            using var directory = new TemporaryDirectory();
            string left = directory.GetPath("left.txt");
            string right = directory.GetPath("right.txt");
            File.WriteAllText(left, "one");
            File.WriteAllText(right, "two");

            Assert.Equal(FileComparisonMode.Text, FileTypeClassifier.GetComparisonMode(left, right));
        }

        [Fact]
        public void GetComparisonMode_ForTwoCsvFiles_ReturnsCsv()
        {
            using var directory = new TemporaryDirectory();
            string left = directory.GetPath("left.csv");
            string right = directory.GetPath("right.csv");
            File.WriteAllText(left, "a,b");
            File.WriteAllText(right, "c,d");

            Assert.Equal(FileComparisonMode.Csv, FileTypeClassifier.GetComparisonMode(left, right));
        }

        [Fact]
        public void GetComparisonMode_ForTextAndImage_ReturnsBinary()
        {
            using var directory = new TemporaryDirectory();
            string text = directory.GetPath("left.txt");
            string image = directory.GetPath("right.png");
            File.WriteAllText(text, "one");
            File.WriteAllBytes(image, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            Assert.Equal(FileComparisonMode.Binary, FileTypeClassifier.GetComparisonMode(text, image));
        }

        private static void CreateSampleZip(string path, string entryName, string content)
        {
            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }
    }
}
