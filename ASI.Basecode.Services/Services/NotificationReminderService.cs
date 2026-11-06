using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using N = ASI.Basecode.Resources.Constants.DomainValues.NotificationTypes;

namespace ASI.Basecode.Services.Services
{
    public class NotificationReminderService
    {
        private readonly AsiBasecodeDBContext _db;
        private readonly INotificationDispatcher _dispatcher;
        private readonly NotificationOptions _options;
        private readonly TimeProvider _clock;
        public NotificationReminderService(IUnitOfWork unitOfWork, INotificationDispatcher dispatcher,
            IOptions<NotificationOptions> options, TimeProvider clock)
        {
            _db = unitOfWork.Context;
            _dispatcher = dispatcher;
            _options = options.Value;
            _clock = clock;
        }
        public async Task ScanAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled) return;
            var now = _clock.GetUtcNow().UtcDateTime;
            var until = now.AddHours(Math.Clamp(_options.DueSoonHours, 1, 168));
            long cursor = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var ids = await _db.Reservations.AsNoTracking().Where(x => x.ReservationId > cursor &&
                    x.ReleaseRecord != null && x.ReleaseRecord.ReturnRecord == null && x.ReservationEnd <= until)
                    .OrderBy(x => x.ReservationId).Select(x => x.ReservationId).Take(100).ToListAsync(cancellationToken);
                if (ids.Count == 0) break;
                foreach (var id in ids)
                {
                    await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                    {
                        _db.ChangeTracker.Clear();
                        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                        // Recheck inside the transaction: a completed return must not create a new reminder.
                        var end = await _db.Reservations.Where(x => x.ReservationId == id &&
                            x.ReleaseRecord != null && x.ReleaseRecord.ReturnRecord == null && x.ReservationEnd <= until)
                            .Select(x => (DateTime?)x.ReservationEnd).SingleOrDefaultAsync(cancellationToken);
                        if (end.HasValue)
                        {
                            var type = end.Value < now ? N.Overdue : N.DueSoon;
                            _dispatcher.StageReservation(id, type, type == N.Overdue
                                ? "Please return the equipment or contact the equipment desk."
                                : "Your equipment is due soon. Check your reservation for the return date.");
                            await _db.SaveChangesAsync(cancellationToken);
                        }
                        await transaction.CommitAsync(cancellationToken);
                    });
                }
                cursor = ids[^1];
            }
        }
    }
}
