using System.Text;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class TextFileEncodingServiceTests
    {
        private static Encoding Detect(byte[] prefix)
        {
            return TextFileEncodingService.DetectEncoding(prefix, prefix?.Length ?? 0);
        }

        [Fact]
        public void DetectEncoding_NullPrefix_ReturnsUtf8WithoutBom()
        {
            Encoding result = TextFileEncodingService.DetectEncoding(null, 0);
            Assert.Equal("utf-8", result.WebName);
            Assert.Empty(result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_EmptyPrefix_ReturnsUtf8WithoutBom()
        {
            Encoding result = Detect(Array.Empty<byte>());
            Assert.Equal("utf-8", result.WebName);
            Assert.Empty(result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_PlainAsciiText_ReturnsUtf8WithoutBom()
        {
            Encoding result = Detect(Encoding.ASCII.GetBytes("plain ascii text"));
            Assert.Equal("utf-8", result.WebName);
            Assert.Empty(result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_Utf8Bom_ReturnsUtf8WithBom()
        {
            Encoding result = Detect(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' });
            Assert.Equal("utf-8", result.WebName);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_Utf16LittleEndianBom_ReturnsUtf16()
        {
            Encoding result = Detect(new byte[] { 0xFF, 0xFE, 0x3C, 0x00 });
            Assert.Equal("utf-16", result.WebName);
        }

        [Fact]
        public void DetectEncoding_Utf16BigEndianBom_ReturnsBigEndianUtf16()
        {
            Encoding result = Detect(new byte[] { 0xFE, 0xFF, 0x00, 0x3C });
            Assert.Equal("utf-16BE", result.WebName);
        }

        [Fact]
        public void DetectEncoding_Utf32LittleEndianBom_IsNotMistakenForUtf16()
        {
            Encoding result = Detect(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00 });
            Assert.Equal("utf-32", result.WebName);
        }

        [Fact]
        public void DetectEncoding_Utf32BigEndianBom_ReturnsUtf32BigEndian()
        {
            Encoding result = Detect(new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x3C });
            Assert.Equal("utf-32BE", result.WebName);
        }

        [Fact]
        public void DetectEncoding_Utf16LittleEndianWithoutBomXmlDeclaration_ReturnsUtf16()
        {
            Encoding result = Detect(new byte[] { 0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00 });
            Assert.Equal("utf-16", result.WebName);
            Assert.Empty(result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_XmlDeclarationWithWindows1251_UsesDeclaredEncoding()
        {
            byte[] prefix = Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"windows-1251\"?><root />");
            Encoding result = Detect(prefix);
            Assert.Equal("windows-1251", result.WebName);
        }

        [Fact]
        public void DetectEncoding_XmlDeclarationWithoutEncodingAttribute_ReturnsUtf8WithoutBom()
        {
            byte[] prefix = Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><root />");
            Encoding result = Detect(prefix);
            Assert.Equal("utf-8", result.WebName);
            Assert.Empty(result.GetPreamble());
        }

        [Fact]
        public void DetectEncoding_UnknownDeclaredEncoding_ReturnsUtf8WithoutBom()
        {
            byte[] prefix = Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"no-such-encoding\"?><root />");
            Encoding result = Detect(prefix);
            Assert.Equal("utf-8", result.WebName);
        }

        [Fact]
        public void OpenReader_XmlFileWithWindows1251Declaration_ReadsDeclaredEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var directory = new TemporaryDirectory();
            string path = directory.GetPath("book.fb2");
            byte[] payload = Encoding.GetEncoding("windows-1251").GetBytes(
                "<?xml version=\"1.0\" encoding=\"windows-1251\"?><root>Привет мир</root>");
            File.WriteAllBytes(path, payload);

            string text = TextFileEncodingService.ReadAllText(path, out Encoding detected);

            Assert.Equal("windows-1251", detected.WebName);
            Assert.Contains("Привет мир", text);
        }
    }
}
