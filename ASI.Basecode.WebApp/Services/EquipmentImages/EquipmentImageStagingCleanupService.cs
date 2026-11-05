using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Services.EquipmentImages
{
    public sealed class EquipmentImageStagingCleanupService : BackgroundService
    {
        private readonly IEquipmentImageStorage _storage;
        private readonly ILogger<EquipmentImageStagingCleanupService> _logger;

        public EquipmentImageStagingCleanupService(
            IEquipmentImageStorage storage,
            ILogger<EquipmentImageStagingCleanupService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _storage.DeleteExpiredStagedFiles();
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not clean expired staged equipment image uploads.");
                }
            }
        }
    }
}
