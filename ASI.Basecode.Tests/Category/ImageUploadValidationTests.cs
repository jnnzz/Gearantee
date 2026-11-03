using ASI.Basecode.Services.Utilities;
using System.IO;
using System.Text;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class ImageUploadValidationTests
    {
        private static Stream StreamOf(params byte[] bytes) => new MemoryStream(bytes);

        private static byte[] HeaderFor(string contentType)
        {
            switch (contentType)
            {
                case "image/jpeg": return JpegHeader;
                case "image/png": return PngHeader;
                case "image/webp": return WebPHeader;
                default: return PngHeader;
            }
        }

        private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        private static readonly byte[] PngHeader =
            { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        private static readonly byte[] WebPHeader =
            { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };

        [Theory]
        [InlineData("photo.jpg", "image/jpeg")]
        [InlineData("photo.jpeg", "image/jpeg")]
        [InlineData("photo.png", "image/png")]
        [InlineData("photo.webp", "image/webp")]
        public void Validate_AcceptsEachSupportedFormat(string fileName, string contentType)
        {
            using var stream = StreamOf(HeaderFor(contentType));
            var result = ImageUploadValidation.Validate(stream, fileName, contentType);

            Assert.True(result.IsValid, result.Error);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(0, stream.Position);
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
