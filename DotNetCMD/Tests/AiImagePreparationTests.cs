using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class AiImagePreparationTests
    {
        [Fact]
        public async Task PrepareImages_ResizesUploadAndLeavesOriginalUnchanged()
        {
            using var directory = new TemporaryDirectory();
            string sourcePath = directory.GetPath("photo.png");
            using (var bitmap = new Bitmap(1600, 800))
            {
                using Graphics graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.DarkSlateBlue);
                bitmap.Save(sourcePath, ImageFormat.Png);
            }

            byte[] original = File.ReadAllBytes(sourcePath);
            byte[] originalHash = SHA256.HashData(original);

            IReadOnlyList<AiPreparedImage> prepared =
                await AiOrganizationService.PrepareImagesForPreviewAsync(
                    new[] { sourcePath }, CancellationToken.None);

            AiPreparedImage image = Assert.Single(prepared);
            Assert.Equal(Path.GetFullPath(sourcePath), image.FullPath);
            Assert.Equal(original.Length, image.SourceBytes);
            Assert.Equal(1600, image.SourceWidth);
            Assert.Equal(800, image.SourceHeight);
            Assert.Equal(1280, image.PreparedWidth);
            Assert.Equal(640, image.PreparedHeight);
            Assert.InRange(image.JpegBytes.Length, 1, 2 * 1024 * 1024);
            Assert.InRange(image.SourcePreviewJpegBytes.Length, 1, 2 * 1024 * 1024);

            using var uploadStream = new MemoryStream(image.JpegBytes);
            using Image upload = Image.FromStream(uploadStream);
            Assert.Equal(1280, upload.Width);
            Assert.Equal(640, upload.Height);
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(sourcePath)));
        }

        [Fact]
        public async Task PrepareImages_SkipsSourceAboveSizeLimit()
        {
            using var directory = new TemporaryDirectory();
            string sourcePath = directory.GetPath("large.png");
            using (FileStream stream = File.Create(sourcePath))
                stream.SetLength(8L * 1024 * 1024 + 1);

            IReadOnlyList<AiPreparedImage> prepared =
                await AiOrganizationService.PrepareImagesForPreviewAsync(
                    new[] { sourcePath }, CancellationToken.None);

            Assert.Empty(prepared);
            Assert.Equal(8L * 1024 * 1024 + 1, new FileInfo(sourcePath).Length);
        }

        [Fact]
        public async Task PrepareImages_HonorsCancellationBeforeOpeningFiles()
        {
            using var directory = new TemporaryDirectory();
            string sourcePath = directory.GetPath("photo.png");
            File.WriteAllText(sourcePath, "placeholder");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AiOrganizationService.PrepareImagesForPreviewAsync(
                    new[] { sourcePath }, cancellation.Token));
        }
    }
}
