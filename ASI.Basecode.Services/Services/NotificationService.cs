using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repository;
        private readonly TimeProvider _clock;
        public NotificationService(INotificationRepository repository, TimeProvider clock)
        {
            _repository = repository;
            _clock = clock;
        }
        private IQueryable<Notification> ForUser(string userId) => _repository.Query()
            .Where(x => x.UserId == userId && x.User.IsActive);

        public async Task<NotificationPage> GetPageAsync(string userId, int page, CancellationToken cancellationToken = default)
        {
            var query = ForUser(userId).AsNoTracking();
            var result = new NotificationPage { TotalCount = await query.CountAsync(cancellationToken) };
            result.Page = Math.Clamp(page, 1, result.TotalPages);
            result.Items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.NotificationId)
                .Skip((result.Page - 1) * 10).Take(10)
                .Select(x => new NotificationItem { NotificationId = x.NotificationId, Title = x.Title,
                    Body = x.Body, LinkUrl = x.LinkUrl, CreatedAt = x.CreatedAt, ReadAt = x.ReadAt })
                .ToListAsync(cancellationToken);
            return result;
        }
        public Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            ForUser(userId).CountAsync(x => x.ReadAt == null, cancellationToken);

        public Task MarkReadAsync(string userId, long? id, CancellationToken cancellationToken = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            return ForUser(userId).Where(x => x.ReadAt == null && (!id.HasValue || x.NotificationId == id.Value))
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.ReadAt, (DateTime?)now), cancellationToken);
        }
    }
}
