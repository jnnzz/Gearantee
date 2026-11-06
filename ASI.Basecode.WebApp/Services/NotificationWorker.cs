using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.WebApp.Services
{
    public class NotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<NotificationWorker> _logger;
        private readonly NotificationOptions _options;
        public NotificationWorker(IServiceScopeFactory scopes, ILogger<NotificationWorker> logger, IOptions<NotificationOptions> options)
        { _scopes = scopes; _logger = logger; _options = options.Value; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled) return;
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationReminderService>().ScanAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { _logger.LogError(exception, "Notification reminder scan failed; will retry."); }
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationEmailProcessor>().ProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { _logger.LogError(exception, "Notification email processing failed; will retry."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
