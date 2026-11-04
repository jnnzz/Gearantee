using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Services.EquipmentImages
{
    public sealed class EquipmentImageStorage : IEquipmentImageStorage
    {
        public const long MaxImageBytes = 5_000_000;
        private const int MaxImageDimension = 6_000;
        private const long MaxImagePixels = 8_000_000;
        private const string PublicRoutePrefix = "/equipment-images/";
        private const string StagedRoutePrefix = "/equipment-images/staged/";
        private static readonly Regex ManagedFileName = new(
            @"\A[a-f0-9]{32}\.(jpg|png)\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly SemaphoreSlim DecodeSlots = new(2, 2);

        private readonly string _storagePath;
        private readonly string _stagingPath;
        private readonly TimeSpan _stagingLifetime;

        public EquipmentImageStorage(
            IWebHostEnvironment environment,
            IOptions<EquipmentImageStorageOptions> options)
        {
            var configuredPath = options.Value.StoragePath;
            _storagePath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
            _stagingPath = Path.Combine(_storagePath, ".staging");
            _stagingLifetime = TimeSpan.FromHours(Math.Clamp(options.Value.StagingLifetimeHours, 1, 720));
        }

        public async Task<EquipmentImageSaveResult> SaveAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                return EquipmentImageSaveResult.Failure("Choose a JPEG or PNG image to upload.");
            }

            if (file.Length > MaxImageBytes)
            {
                return EquipmentImageSaveResult.Failure("The image must be 5 MB or smaller.");
            }

            var extension = Path.GetExtension(file.FileName);
            var contentType = file.ContentType?.Trim();
            var extensionMatches =
                (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                    string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase);
            if (!extensionMatches)
            {
                return EquipmentImageSaveResult.Failure("Only JPEG and PNG images are supported.");
            }

            byte[] bytes;
            await using (var input = new MemoryStream())
            {
                await file.CopyToAsync(input, cancellationToken);
                if (input.Length == 0 || input.Length > MaxImageBytes)
                {
                    return EquipmentImageSaveResult.Failure("The image must be 5 MB or smaller.");
                }

                bytes = input.ToArray();
            }

            await DecodeSlots.WaitAsync(cancellationToken);
            try
            {
                return await DecodeAndStoreAsync(bytes, extension, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return EquipmentImageSaveResult.Failure("The image could not be validated or saved. Check the file and storage permissions, then try again.");
            }
            finally
            {
                DecodeSlots.Release();
            }
        }

        private async Task<EquipmentImageSaveResult> DecodeAndStoreAsync(
            byte[] bytes,
            string suppliedExtension,
            CancellationToken cancellationToken)
        {
            using var source = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(source);
            if (codec == null ||
                (codec.EncodedFormat != SKEncodedImageFormat.Jpeg &&
                 codec.EncodedFormat != SKEncodedImageFormat.Png))
            {
                return EquipmentImageSaveResult.Failure("The uploaded file is not a valid JPEG or PNG image.");
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 ||
                info.Width > MaxImageDimension || info.Height > MaxImageDimension ||
                (long)info.Width * info.Height > MaxImagePixels)
            {
                return EquipmentImageSaveResult.Failure("Image dimensions must be at most 6,000 pixels per side and 8 megapixels total.");
            }

            using var bitmap = SKBitmap.Decode(source);
            if (bitmap == null || bitmap.Width != info.Width || bitmap.Height != info.Height)
            {
                return EquipmentImageSaveResult.Failure("The uploaded file could not be decoded as an image.");
            }

            var isPng = codec.EncodedFormat == SKEncodedImageFormat.Png;
            var extension = isPng ? ".png" : ".jpg";
            if (isPng != string.Equals(suppliedExtension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return EquipmentImageSaveResult.Failure("The file extension does not match the image content.");
            }

            Directory.CreateDirectory(_storagePath);
            var fileName = Guid.NewGuid().ToString("N") + extension;
            Directory.CreateDirectory(_stagingPath);
            var path = GetSafeStagedFilePath(fileName);
            var created = false;
            try
            {
                await using var output = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);
                created = true;
                await output.WriteAsync(bytes.AsMemory(), cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            catch
            {
                if (created && File.Exists(path))
                {
                    File.Delete(path);
                }

                throw;
            }

            return EquipmentImageSaveResult.Success(StagedRoutePrefix + fileName);
        }

        public bool IsManagedImageUrl(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(PublicRoutePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var fileName = imageUrl[PublicRoutePrefix.Length..];
            return ManagedFileName.IsMatch(fileName);
        }

        public bool IsStagedImageUrl(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(StagedRoutePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return ManagedFileName.IsMatch(imageUrl[StagedRoutePrefix.Length..]);
        }

        public bool Exists(string imageUrl)
        {
            if (IsManagedImageUrl(imageUrl))
            {
                return File.Exists(GetSafeFilePath(imageUrl[PublicRoutePrefix.Length..]));
            }

            return IsStagedImageUrl(imageUrl) &&
                File.Exists(GetSafeStagedFilePath(imageUrl[StagedRoutePrefix.Length..]));
        }

        public string GetSafeFilePath(string fileName)
        {
            if (fileName == null || !ManagedFileName.IsMatch(fileName))
            {
                throw new ArgumentException("The image file name is invalid.", nameof(fileName));
            }

            var path = Path.GetFullPath(Path.Combine(_storagePath, fileName));
            var relativePath = Path.GetRelativePath(_storagePath, path);
            if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("The image path is invalid.", nameof(fileName));
            }

            return path;
        }

        public string GetSafeStagedFilePath(string fileName)
        {
            if (fileName == null || !ManagedFileName.IsMatch(fileName))
            {
                throw new ArgumentException("The staged image file name is invalid.", nameof(fileName));
            }

            var path = Path.GetFullPath(Path.Combine(_stagingPath, fileName));
            var relativePath = Path.GetRelativePath(_stagingPath, path);
            if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("The staged image path is invalid.", nameof(fileName));
            }

            return path;
        }

        public Task<string> PromoteAsync(
            string stagedImageUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsStagedImageUrl(stagedImageUrl) || !Exists(stagedImageUrl))
            {
                return Task.FromResult<string>(null);
            }

            var stagedName = stagedImageUrl[StagedRoutePrefix.Length..];
            var extension = Path.GetExtension(stagedName);
            var finalName = Guid.NewGuid().ToString("N") + extension;
            Directory.CreateDirectory(_storagePath);
            File.Move(GetSafeStagedFilePath(stagedName), GetSafeFilePath(finalName));
            return Task.FromResult(PublicRoutePrefix + finalName);
        }

        public Task<string> RevertToStagedAsync(
            string imageUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsManagedImageUrl(imageUrl) || !Exists(imageUrl))
            {
                return Task.FromResult<string>(null);
            }

            var fileName = imageUrl[PublicRoutePrefix.Length..];
            var stagedName = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName);
            Directory.CreateDirectory(_stagingPath);
            File.Move(GetSafeFilePath(fileName), GetSafeStagedFilePath(stagedName));
            return Task.FromResult(StagedRoutePrefix + stagedName);
        }

        public string GetContentType(string fileName) =>
            Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";

        public void Delete(string imageUrl)
        {
            if (!IsManagedImageUrl(imageUrl) && !IsStagedImageUrl(imageUrl))
            {
                return;
            }

            var path = IsManagedImageUrl(imageUrl)
                ? GetSafeFilePath(imageUrl[PublicRoutePrefix.Length..])
                : GetSafeStagedFilePath(imageUrl[StagedRoutePrefix.Length..]);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void DeleteExpiredStagedFiles()
        {
            if (!Directory.Exists(_stagingPath))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - _stagingLifetime;
            foreach (var filePath in Directory.EnumerateFiles(_stagingPath, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(filePath);
                if (!ManagedFileName.IsMatch(fileName))
                {
                    continue;
                }

                try
                {
                    if (File.GetLastWriteTimeUtc(filePath) < cutoff)
                    {
                        File.Delete(GetSafeStagedFilePath(fileName));
                    }
                }
                catch (IOException)
                {
                    // A staged upload still being read or replaced can be retried next pass.
                }
                catch (UnauthorizedAccessException)
                {
                    // A deployment can correct folder permissions without failing app startup.
                }
            }
        }
    }
}
