using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.Services.Services
{
    public class NotificationEmailProcessor
    {
        private readonly AsiBasecodeDBContext _db;
        private readonly IBrevoEmailSender _sender;
        private readonly NotificationOptions _options;
        private readonly TimeProvider _clock;
        public NotificationEmailProcessor(IUnitOfWork unitOfWork, IBrevoEmailSender sender,
            IOptions<NotificationOptions> options, TimeProvider clock)
        {
            _db = unitOfWork.Context;
            _sender = sender;
            _options = options.Value;
            _clock = clock;
        }

        public async Task ProcessAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled) return;
            var now = _clock.GetUtcNow().UtcDateTime;
            var ids = await _db.Notifications.AsNoTracking().Where(x => x.EmailRequested && x.EmailStatus == "Pending" &&
                (x.EmailNextAttemptAt == null || x.EmailNextAttemptAt <= now) &&
                (x.EmailLeaseExpiresAt == null || x.EmailLeaseExpiresAt <= now))
                .OrderBy(x => x.NotificationId).Select(x => x.NotificationId).Take(25).ToListAsync(cancellationToken);
            foreach (var id in ids)
            {
                now = _clock.GetUtcNow().UtcDateTime;
                var token = Guid.NewGuid().ToString("N");
                var expires = now.AddMinutes(5);
                var claimed = await _db.Notifications.Where(x => x.NotificationId == id && x.EmailStatus == "Pending" &&
                    (x.EmailNextAttemptAt == null || x.EmailNextAttemptAt <= now) &&
                    (x.EmailLeaseExpiresAt == null || x.EmailLeaseExpiresAt <= now))
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.EmailLeaseToken, token)
                        .SetProperty(x => x.EmailLeaseExpiresAt, (DateTime?)expires), cancellationToken);
                if (claimed == 0) continue;
                var row = await _db.Notifications.AsNoTracking().Include(x => x.User)
                    .SingleOrDefaultAsync(x => x.NotificationId == id && x.EmailLeaseToken == token, cancellationToken);
                if (row == null) continue;
                var status = "Sent";
                string error = null;
                var attempts = row.EmailAttempts;
                DateTime? sentAt = null;
                DateTime? next = null;
                if (!row.User.IsActive || string.IsNullOrWhiteSpace(row.User.Email))
                {
                    status = "Failed";
                    error = "Recipient is inactive or has no registered email.";
                }
                else
                {
                    attempts++;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(30));
                        var html = $"<h1>{WebUtility.HtmlEncode(row.Title)}</h1><p>{WebUtility.HtmlEncode(row.Body)}</p><p>Sign in to Gearantee to view your reservation.</p>";
                        await _sender.SendAsync(row.User.Email, row.User.DisplayName, row.Title, html, timeout.Token);
                        sentAt = _clock.GetUtcNow().UtcDateTime;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        status = attempts >= 5 ? "Failed" : "Pending";
                        // Provider exceptions may contain recipient data; retain only a safe operational message.
                        error = "Email delivery failed or timed out.";
                        next = _clock.GetUtcNow().UtcDateTime.AddMinutes(Math.Pow(2, attempts));
                    }
                }
                await _db.Notifications.Where(x => x.NotificationId == id && x.EmailLeaseToken == token)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.EmailStatus, status)
                        .SetProperty(x => x.EmailAttempts, attempts).SetProperty(x => x.EmailLastError, error)
                        .SetProperty(x => x.EmailSentAt, sentAt).SetProperty(x => x.EmailNextAttemptAt, next)
                        .SetProperty(x => x.EmailLeaseToken, (string)null).SetProperty(x => x.EmailLeaseExpiresAt, (DateTime?)null), cancellationToken);
            }
        }
    }
}
