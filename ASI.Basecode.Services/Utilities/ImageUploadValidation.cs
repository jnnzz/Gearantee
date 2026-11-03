using System;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Utilities
{
    /// <summary>
    /// Authoritative image validation for uploads: extension, content type, magic bytes,
    /// and size. The client's declared content type is never trusted. GIF is deliberately
    /// not supported. All checks run on the stream without buffering the whole file.
    /// </summary>
    public static class ImageUploadValidation
    {
        public const long DefaultMaxFileBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>Result of validating an upload candidate.</summary>
        public sealed class ImageValidationResult
        {
            public bool IsValid { get; private set; }
            public string Error { get; private set; }

            /// <summary>Content type derived from the sniffed magic bytes, not the client header.</summary>
            public string ContentType { get; private set; }

            public static ImageValidationResult Ok(string contentType) =>
                new() { IsValid = true, ContentType = contentType };

            public static ImageValidationResult Invalid(string error) =>
                new() { IsValid = false, Error = error };
        }

        public static bool IsAllowedExtension(string fileName) =>
            AllowedExtensions.Contains(
                Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant());

        /// <summary>
        /// Validates the candidate upload and positions the stream at its start ready for
        /// storage. The stream must be seekable.
        /// </summary>
        public static ImageValidationResult Validate(
            Stream content,
            string fileName,
            string declaredContentType,
            long maxFileBytes = DefaultMaxFileBytes)
        {
            if (content == null)
            {
                return ImageValidationResult.Invalid("Choose an image file.");
            }

            if (!IsAllowedExtension(fileName))
            {
                return ImageValidationResult.Invalid(
                    "Only .jpg, .jpeg, .png, and .webp images are allowed.");
            }

            if (content.Length <= 0)
            {
                return ImageValidationResult.Invalid("The selected file is empty.");
            }

            if (content.Length > maxFileBytes)
            {
                return ImageValidationResult.Invalid(
                    $"Image size exceeds the maximum allowed size of {maxFileBytes / (1024 * 1024)} MB. Please select a file {maxFileBytes / (1024 * 1024)} MB or smaller.");
            }

            var sniffed = SniffContentType(content);
            if (sniffed == null)
            {
                return ImageValidationResult.Invalid(
                    "That file is not a valid JPEG, PNG, or WebP image.");
            }

            if (!IsCompatibleType(sniffed, declaredContentType))
            {
                return ImageValidationResult.Invalid(
                    "The file's contents do not match its image type.");
            }

            content.Seek(0, SeekOrigin.Begin);
            return ImageValidationResult.Ok(sniffed);
        }

        private static bool IsCompatibleType(string sniffed, string declared)
        {
            if (string.Equals(sniffed, declared, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(sniffed, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(declared, "image/jpg", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(declared, "image/pjpeg", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(sniffed, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(declared, "image/x-png", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>Reads the magic bytes and maps them to a canonical content type.</summary>
        private static string SniffContentType(Stream content)
        {
            content.Seek(0, SeekOrigin.Begin);
            Span<byte> header = stackalloc byte[12];
            var read = 0;
            while (read < header.Length)
            {
                var chunk = content.Read(header[read..]);
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            content.Seek(0, SeekOrigin.Begin);

            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return "image/png";
            }

            if (read >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            {
                return "image/webp";
            }

            return null;
        }
    }
}
