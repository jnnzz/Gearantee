using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Stream-based object storage for equipment images. Implementations return the
    /// public URL of the stored object; the database persists only that URL.
    /// </summary>
    public interface IImageStorageService
    {
        /// <summary>
        /// Uploads the stream as a new object and returns its public URL.
        /// Throws when the storage call fails.
        /// </summary>
        Task<string> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the object behind a previously returned public URL. URLs that do not
        /// belong to this storage (legacy or same-site paths) are ignored; a missing
        /// object counts as deleted.
        /// </summary>
        Task DeleteAsync(string publicUrl, CancellationToken cancellationToken = default);
    }
}
