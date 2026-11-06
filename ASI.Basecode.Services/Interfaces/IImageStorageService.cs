using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Stream-based object storage for equipment images. <see cref="UploadAsync"/> returns a
    /// stable object reference that the database persists; because the bucket is private,
    /// callers must use <see cref="CreateSignedUrlAsync"/> to build a display URL.
    /// </summary>
    public interface IImageStorageService
    {
        /// <summary>
        /// Uploads the stream as a new object and returns its stable object reference
        /// (a URL that points at the stored object). Throws when the storage call fails.
        /// </summary>
        Task<string> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the object behind a previously returned reference. Values that do not
        /// belong to this storage (legacy external URLs or same-site paths) are ignored;
        /// a missing object counts as deleted.
        /// </summary>
        Task DeleteAsync(string imageReference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a time-limited URL that can be embedded in markup for a stored reference.
        /// Legacy external URLs and same-site paths are returned unchanged; when the value
        /// cannot be signed the original value is returned so the caller degrades gracefully.
        /// </summary>
        Task<string> CreateSignedUrlAsync(
            string imageReference,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);
    }
}
