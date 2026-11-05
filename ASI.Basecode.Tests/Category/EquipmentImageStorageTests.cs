using ASI.Basecode.WebApp.Services.EquipmentImages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class EquipmentImageStorageTests
    {
        [Fact]
        public async Task SaveAsync_AcceptsDecodedJpegAndStagesItWithGeneratedName()
        {
            await using var fixture = new StorageFixture();
            var jpeg = CreateImageBytes(SKEncodedImageFormat.Jpeg);
            using var stream = new MemoryStream(jpeg);
            var file = CreateFile(stream, "camera.jpeg", "image/jpeg");

            var result = await fixture.Storage.SaveAsync(file);

            Assert.True(result.Succeeded, result.Error);
            Assert.Matches(@"\A/equipment-images/staged/[a-f0-9]{32}\.jpg\z", result.ImageUrl);
            Assert.True(fixture.Storage.Exists(result.ImageUrl));
            Assert.Equal("image/jpeg", fixture.Storage.GetContentType(Path.GetFileName(result.ImageUrl)));
        }

        [Fact]
        public async Task SaveAsync_RejectsValidPngBytesWithForgedJpegExtensionAndContentType()
        {
            await using var fixture = new StorageFixture();
            var png = CreateImageBytes(SKEncodedImageFormat.Png);
            using var stream = new MemoryStream(png);
            var file = CreateFile(stream, "renamed.jpg", "image/jpeg");

            var result = await fixture.Storage.SaveAsync(file);

            Assert.False(result.Succeeded);
            Assert.Contains("extension does not match", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(fixture.StorageRoot));
        }

        [Theory]
        [InlineData("../outside.png")]
        [InlineData("..\\outside.png")]
        [InlineData("0123456789abcdef0123456789abcdef.png/../outside.png")]
        public async Task SafePathMethods_RejectTraversalNames(string fileName)
        {
            await using var fixture = new StorageFixture();

            Assert.Throws<ArgumentException>(() => fixture.Storage.GetSafeFilePath(fileName));
            Assert.Throws<ArgumentException>(() => fixture.Storage.GetSafeStagedFilePath(fileName));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "outside.png")));
        }

        [Fact]
        public async Task DeleteExpiredStagedFiles_RemovesExpiredUploadsAndKeepsRecentOnes()
        {
            await using var fixture = new StorageFixture(stagingLifetimeHours: 1);
            var png = CreateImageBytes(SKEncodedImageFormat.Png);
            using var staleStream = new MemoryStream(png);
            var stale = await fixture.Storage.SaveAsync(CreateFile(staleStream, "stale.png", "image/png"));
            Assert.True(stale.Succeeded, stale.Error);
            var stalePath = fixture.Storage.GetSafeStagedFilePath(Path.GetFileName(stale.ImageUrl));
            File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddHours(-2));

            using var recentStream = new MemoryStream(png);
            var recent = await fixture.Storage.SaveAsync(CreateFile(recentStream, "recent.png", "image/png"));
            Assert.True(recent.Succeeded, recent.Error);

            fixture.Storage.DeleteExpiredStagedFiles();

            Assert.False(fixture.Storage.Exists(stale.ImageUrl));
            Assert.True(fixture.Storage.Exists(recent.ImageUrl));
        }

        private static FormFile CreateFile(Stream stream, string fileName, string contentType)
        {
            var file = new FormFile(stream, 0, stream.Length, "imageFile", fileName)
            {
                Headers = new HeaderDictionary()
            };
            file.Headers.ContentType = contentType;
            return file;
        }

        private static byte[] CreateImageBytes(SKEncodedImageFormat format)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
            bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(format, 100);
            return encoded.ToArray();
        }

        private sealed class StorageFixture : IAsyncDisposable
        {
            private readonly string _storagePath;

            public string Root { get; } = Path.Combine(
                Path.GetTempPath(), "Gearantee-image-storage-tests-" + Guid.NewGuid().ToString("N"));
            public string StorageRoot => _storagePath;
            public EquipmentImageStorage Storage { get; }

            public StorageFixture(int stagingLifetimeHours = 24)
            {
                Directory.CreateDirectory(Root);
                _storagePath = Path.Combine(Root, "images");
                Storage = new EquipmentImageStorage(
                    new TestWebHostEnvironment(Root),
                    Options.Create(new EquipmentImageStorageOptions
                    {
                        StoragePath = _storagePath,
                        StagingLifetimeHours = stagingLifetimeHours
                    }));
            }

            public ValueTask DisposeAsync()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "EquipmentImageStorageTests";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = Path.GetTempPath();
            public string EnvironmentName { get; set; } = "Testing";
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

            public TestWebHostEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;
        }
    }
}
