using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationPage> GetPageAsync(string userId, int page, CancellationToken cancellationToken = default);
        Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken = default);
        Task MarkReadAsync(string userId, long? id, CancellationToken cancellationToken = default);
    }
}
