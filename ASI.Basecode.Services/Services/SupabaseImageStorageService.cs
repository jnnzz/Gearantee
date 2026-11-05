using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Uploads equipment images to a public Supabase Storage bucket over a typed
    /// HttpClient and returns the public object URL. Only the URL is persisted by
    /// callers; the service_role key stays in configuration and is never logged.
    /// </summary>
    public class SupabaseImageStorageService : IImageStorageService
    {
        private const string StorageObjectTemplate = "storage/v1/object/{0}/{1}";
        private const string StorageSignTemplate = "storage/v1/object/sign/{0}/{1}";
        private const string ObjectUrlTemplate = "storage/v1/object/{0}/{1}";

        private static readonly Regex StorageUrlPattern = new(
            @"^https?://[^/]+/storage/v1/object/(?:public/)?(?<bucket>[^/]+)/(?<path>.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly SupabaseOptions _options;
        private readonly ILogger<SupabaseImageStorageService> _logger;

        public SupabaseImageStorageService(
            HttpClient httpClient,
            IOptions<SupabaseOptions> options,
            ILogger<SupabaseImageStorageService> logger = null)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!_options.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Supabase storage is not configured. Set Supabase:Url and Supabase:ServiceRoleKey.");
            }

            var extension = Path.GetExtension(fileName ?? string.Empty);
            var objectPath = BuildObjectPath(extension);
            var requestUri = ResolveRequestUri(string.Format(
                CultureInfo.InvariantCulture, StorageObjectTemplate, _options.Bucket, objectPath));

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _options.ServiceRoleKey);
            request.Headers.Add("x-upsert", "false");
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content.Headers.ContentLength = content.CanSeek ? content.Length : null;

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Log the status only: the body may echo configuration and the
                // authorization header must never be logged.
                _logger?.LogError(
                    "Supabase image upload failed with status {StatusCode} for object {ObjectPath}.",
                    (int)response.StatusCode,
                    objectPath);
                throw new InvalidOperationException(
                    "The image could not be uploaded to storage. Please try again.");
            }

            return BuildObjectUrl(objectPath);
        }

        /// <inheritdoc />
        public async Task<string> CreateSignedUrlAsync(
            string imageReference,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageReference) || !_options.IsConfigured)
            {
                return imageReference;
            }

            var objectPath = TryParseObjectPath(imageReference);
            if (objectPath == null)
            {
                // Legacy external URLs and same-site paths are served as-is.
                return imageReference;
            }

            var requestUri = string.Format(
                CultureInfo.InvariantCulture, StorageSignTemplate, _options.Bucket, objectPath);
            var expiresIn = (int)Math.Clamp(lifetime.TotalSeconds, 1, 604800);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _options.ServiceRoleKey);
            request.Content = new StringContent(
                "{\"expiresIn\":" + expiresIn.ToString(CultureInfo.InvariantCulture) + "}",
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "Supabase signed URL request failed with status {StatusCode} for object {ObjectPath}.",
                    (int)response.StatusCode,
                    objectPath);
                return imageReference;
            }

            using var payload = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            if (!payload.RootElement.TryGetProperty("signedURL", out var signed) ||
                string.IsNullOrWhiteSpace(signed.GetString()))
            {
                return imageReference;
            }

            // Supabase returns a path relative to the Storage API root (e.g.
            // "/object/sign/{bucket}/{path}?token=..."); the client prepends "/storage/v1".
            var signedPath = signed.GetString();
            return signedPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? signedPath
                : _options.Url.TrimEnd('/') + "/storage/v1/" + signedPath.TrimStart('/');
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string publicUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(publicUrl) || !_options.IsConfigured)
            {
                return;
            }

            var objectPath = TryParseObjectPath(publicUrl);
            if (objectPath == null)
            {
                // Legacy external URLs and same-site paths were never stored here.
                return;
            }

            var requestUri = ResolveRequestUri(string.Format(
                CultureInfo.InvariantCulture, StorageObjectTemplate, _options.Bucket, objectPath));

            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _options.ServiceRoleKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return;
            }

            // A failed cleanup must not fail the user request; log and move on.
            _logger?.LogWarning(
                "Supabase image cleanup failed with status {StatusCode} for object {ObjectPath}.",
                (int)response.StatusCode,
                objectPath);
        }

        private Uri ResolveRequestUri(string relativePath)
        {
            if (!string.IsNullOrWhiteSpace(_options.Url) &&
                Uri.TryCreate(_options.Url.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            {
                return new Uri(baseUri, relativePath);
            }

            return new Uri(relativePath, UriKind.RelativeOrAbsolute);
        }

        /// <summary>Stable object reference URL for a path, exposed for tests.</summary>
        public string BuildObjectUrl(string objectPath) => string.Format(
            CultureInfo.InvariantCulture,
            ObjectUrlTemplate,
            _options.Bucket,
            objectPath).Insert(0, _options.Url.TrimEnd('/') + "/");

        /// <summary>Object path for a new upload: items/{guid}{ext}, exposed for tests.</summary>
        public static string BuildObjectPath(string extension)
        {
            extension = string.IsNullOrWhiteSpace(extension)
                ? string.Empty
                : extension.ToLowerInvariant();
            return $"items/{Guid.NewGuid():N}{extension}";
        }

        /// <summary>
        /// Extracts the object path from a URL that points at this project's public
        /// object pattern; null when the URL belongs to someone else.
        /// </summary>
        public string TryParseObjectPath(string publicUrl)
        {
            if (string.IsNullOrWhiteSpace(publicUrl) || !_options.IsConfigured)
            {
                return null;
            }

            var match = StorageUrlPattern.Match(publicUrl);
            if (!match.Success ||
                !string.Equals(
                    match.Groups["bucket"].Value,
                    _options.Bucket,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    new Uri(match.Groups[0].Value).Host,
                    new Uri(_options.Url).Host,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return match.Groups["path"].Value;
        }
    }
}
