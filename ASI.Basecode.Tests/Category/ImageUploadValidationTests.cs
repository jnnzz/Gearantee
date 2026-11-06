using ASI.Basecode.Services.Utilities;
using SkiaSharp;
using System.IO;
using System.Text;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class ImageUploadValidationTests
    {
        private static Stream StreamOf(params byte[] bytes) => new MemoryStream(bytes);

        private static byte[] Encode(string contentType, int width = 1, int height = 1)
        {
            using var bitmap = new SKBitmap(
                new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            var format = contentType switch
            {
                "image/jpeg" => SKEncodedImageFormat.Jpeg,
                "image/webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png,
            };
            using var data = image.Encode(format, 100);
            return data.ToArray();
        }

        private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        private static readonly byte[] PngHeader =
            { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

        [Theory]
        [InlineData("photo.jpg", "image/jpeg")]
        [InlineData("photo.jpeg", "image/jpeg")]
        [InlineData("photo.png", "image/png")]
        [InlineData("photo.webp", "image/webp")]
        public void Validate_AcceptsEachSupportedFormat(string fileName, string contentType)
        {
            using var stream = StreamOf(Encode(contentType));
            var result = ImageUploadValidation.Validate(stream, fileName, contentType);

            Assert.True(result.IsValid, result.Error);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public void Validate_RejectsImageBeyondMaxDimension()
        {
            using var stream = StreamOf(
                Encode("image/png", ImageUploadValidation.MaxDimension + 1, 1));
            var result = ImageUploadValidation.Validate(stream, "photo.png", "image/png");

            Assert.False(result.IsValid);
            Assert.Contains("dimensions", result.Error);
        }

        [Fact]
        public void Validate_RejectsImageBeyondMaxPixels()
        {
            using var stream = StreamOf(Encode("image/png", 6000, 1400));
            var result = ImageUploadValidation.Validate(stream, "photo.png", "image/png");

            Assert.False(result.IsValid);
            Assert.Contains("dimensions", result.Error);
        }

        [Theory]
        [InlineData("photo.gif")]
        [InlineData("photo.bmp")]
        [InlineData("photo.exe")]
        [InlineData("photo")]
        public void Validate_RejectsUnsupportedExtensions(string fileName)
        {
            using var stream = StreamOf(JpegHeader);
            var result = ImageUploadValidation.Validate(stream, fileName, "image/jpeg");

            Assert.False(result.IsValid);
            Assert.Contains("Only .jpg", result.Error);
        }

        [Fact]
        public void Validate_RejectsSpoofedContentType()
        {
            using var stream = StreamOf(PngHeader);
            var result = ImageUploadValidation.Validate(stream, "photo.png", "image/jpeg");

            Assert.False(result.IsValid);
            Assert.Contains("do not match", result.Error);
        }

        [Fact]
        public void Validate_RejectsNonImageBytes()
        {
            var bytes = Encoding.ASCII.GetBytes("This is definitely not an image file.");
            using var stream = StreamOf(bytes);
            var result = ImageUploadValidation.Validate(stream, "photo.png", "image/png");

            Assert.False(result.IsValid);
            Assert.Contains("not a valid", result.Error);
        }

        [Fact]
        public void Validate_RejectsEmptyFile()
        {
            using var stream = StreamOf();
            var result = ImageUploadValidation.Validate(stream, "photo.png", "image/png");

            Assert.False(result.IsValid);
            Assert.Contains("empty", result.Error);
        }

        [Fact]
        public void Validate_RejectsOversizeFile()
        {
            using var stream = StreamOf(JpegHeader);
            var result = ImageUploadValidation.Validate(stream, "photo.jpg", "image/jpeg", maxFileBytes: 4);

            Assert.False(result.IsValid);
            Assert.Contains("smaller", result.Error);
        }

        [Fact]
        public void IsAllowedExtension_CoversSupportedFormatsOnly()
        {
            Assert.True(ImageUploadValidation.IsAllowedExtension("a.PNG"));
            Assert.True(ImageUploadValidation.IsAllowedExtension("a.WebP"));
            Assert.False(ImageUploadValidation.IsAllowedExtension("a.gif"));
            Assert.False(ImageUploadValidation.IsAllowedExtension("a.svg"));
        }
    }
}
