using ASI.Basecode.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Tests.Common
{
    /// <summary>
    /// Offline stand-in for equipment image storage. Records uploads and deletes,
    /// returns deterministic URLs, and can be told to fail the next upload.
    /// </summary>
    public sealed class FakeImageStorageService : IImageStorageService
    {
        private int _counter;

        public List<(string FileName, string ContentType)> Uploads { get; } = new();
        public List<string> DeletedUrls { get; } = new();
        public bool FailNextUpload { get; set; }

        public Task<string> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (FailNextUpload)
            {
                FailNextUpload = false;
                throw new InvalidOperationException("Simulated storage outage.");
            }

            Uploads.Add((fileName, contentType));
            _counter++;
            return Task.FromResult($"https://fake.supabase.co/storage/v1/object/public/equipment-images/items/upload-{_counter:D3}.png");
        }

        public Task DeleteAsync(string publicUrl, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(publicUrl))
            {
                DeletedUrls.Add(publicUrl);
            }

            return Task.CompletedTask;
        }

        public int DeleteCount => DeletedUrls.Count;
        public bool WasDeleted(string url) => DeletedUrls.Any(item => item == url);
    }
}
