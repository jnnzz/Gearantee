using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class SupabaseImageStorageServiceTests
    {
        private static SupabaseOptions Options(
            string url = "https://test-project.supabase.co",
            string bucket = "equipment-images") =>
            new()
            {
                Url = url,
                Bucket = bucket,
                ServiceRoleKey = "test-service-role-key"
            };

        private static SupabaseImageStorageService Service(SupabaseOptions options) =>
            new(
                new System.Net.Http.HttpClient(),
                Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<SupabaseImageStorageService>.Instance);

        [Fact]
        public void BuildObjectPath_UsesItemsFolderGuidAndLowercaseExtension()
        {
            var path = SupabaseImageStorageService.BuildObjectPath(".PNG");

            Assert.StartsWith("items/", path);
            Assert.EndsWith(".png", path);
            var guid = path["items/".Length..^4];
            Assert.Equal(32, guid.Length);
            Assert.True(Guid.TryParseExact(guid, "N", out _));
        }

        [Fact]
        public void BuildObjectUrl_CombinesProjectUrlBucketAndPath()
        {
            var service = Service(Options());
            var url = service.BuildObjectUrl("items/abc123.png");

            Assert.Equal(
                "https://test-project.supabase.co/storage/v1/object/equipment-images/items/abc123.png",
                url);
        }

        [Fact]
        public void TryParseObjectPath_ExtractsPathFromOurObjectUrls()
        {
            var service = Service(Options());
            var url = service.BuildObjectUrl("items/abc123.png");

            Assert.Equal("items/abc123.png", service.TryParseObjectPath(url));
        }

        [Fact]
        public void TryParseObjectPath_StillExtractsLegacyPublicUrls()
        {
            var service = Service(Options());

            Assert.Equal(
                "items/legacy.png",
                service.TryParseObjectPath(
                    "https://test-project.supabase.co/storage/v1/object/public/equipment-images/items/legacy.png"));
        }

        [Fact]
        public async Task CreateSignedUrlAsync_ReturnsSignedUrlForManagedObject()
        {
            var options = Options();
            HttpRequestMessage captured = null;
            var handler = new StubHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"signedURL\":\"/object/sign/equipment-images/items/abc123.png?token=xyz\"}")
                };
            });
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(options.Url.TrimEnd('/') + "/")
            };
            var service = new SupabaseImageStorageService(
                client, Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<SupabaseImageStorageService>.Instance);

            var signed = await service.CreateSignedUrlAsync(
                service.BuildObjectUrl("items/abc123.png"), TimeSpan.FromMinutes(60));

            Assert.Equal(
                "https://test-project.supabase.co/storage/v1/object/sign/equipment-images/items/abc123.png?token=xyz",
                signed);
            Assert.Equal(HttpMethod.Post, captured.Method);
            Assert.Equal(
                "https://test-project.supabase.co/storage/v1/object/sign/equipment-images/items/abc123.png",
                captured.RequestUri.ToString());
        }

        [Fact]
        public async Task CreateSignedUrlAsync_PassesThroughForeignAndSameSiteUrls()
        {
            var service = Service(Options());

            Assert.Equal(
                "/img/legacy/projector.jpg",
                await service.CreateSignedUrlAsync("/img/legacy/projector.jpg", TimeSpan.FromMinutes(60)));
            Assert.Equal(
                "https://example.com/photo.jpg",
                await service.CreateSignedUrlAsync("https://example.com/photo.jpg", TimeSpan.FromMinutes(60)));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
                _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                Task.FromResult(_responder(request));
        }

        [Theory]
        [InlineData("https://other.supabase.co/storage/v1/object/public/equipment-images/items/x.png")]
        [InlineData("https://test-project.supabase.co/storage/v1/object/public/other-bucket/items/x.png")]
        [InlineData("https://test-project.supabase.co/storage/v1/object/authenticated/equipment-images/items/x.png")]
        [InlineData("/img/legacy/projector.jpg")]
        [InlineData("https://example.com/photo.jpg")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParseObjectPath_IgnoresForeignUrls(string url)
        {
            var service = Service(Options());

            Assert.Null(service.TryParseObjectPath(url));
        }

        [Fact]
        public async Task UploadAsync_WithoutConfiguration_Throws()
        {
            var service = Service(new SupabaseOptions { Url = null, ServiceRoleKey = null });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.UploadAsync(new MemoryStream(new byte[] { 1 }), "a.png", "image/png"));
        }

        [Fact]
        public void Options_IsConfigured_RequiresUrlAndKey()
        {
            Assert.False(Options(url: null).IsConfigured);
            Assert.False(Options(url: "").IsConfigured);
            Assert.True(Options().IsConfigured);
        }
    }
}
