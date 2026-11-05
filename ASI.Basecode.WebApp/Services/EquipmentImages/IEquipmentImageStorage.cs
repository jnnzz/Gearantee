using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Services.EquipmentImages
{
    public interface IEquipmentImageStorage
    {
        Task<EquipmentImageSaveResult> SaveAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);

        Task<string> PromoteAsync(string stagedImageUrl, CancellationToken cancellationToken = default);
        Task<string> RevertToStagedAsync(string imageUrl, CancellationToken cancellationToken = default);
        bool IsManagedImageUrl(string imageUrl);
        bool IsStagedImageUrl(string imageUrl);
        bool Exists(string imageUrl);
        string GetSafeFilePath(string fileName);
        string GetSafeStagedFilePath(string fileName);
        string GetContentType(string fileName);
        void Delete(string imageUrl);
        void DeleteExpiredStagedFiles();
    }
}
