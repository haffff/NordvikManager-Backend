using DndOnePlaceManager.Infrastructure.Services.Backup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Checks the backup schedule (Application Settings → Backups) once a minute and makes a
    /// scheduled backup when one is due. The schedule itself lives in the backup folder.
    /// </summary>
    public class ScheduledBackupService : BackgroundService
    {
        private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(15);

        private readonly ScheduledBackupRunner runner;
        private readonly ILogger<ScheduledBackupService> logger;

        public ScheduledBackupService(ScheduledBackupRunner runner, ILogger<ScheduledBackupService> logger)
        {
            this.runner = runner;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Tick);
            var retryAt = DateTime.MinValue;
            do
            {
                if (DateTime.UtcNow < retryAt)
                    continue;
                try
                {
                    if (await runner.RunIfDueAsync(stoppingToken))
                        logger.LogInformation("Scheduled backup created.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // A failed backup (disk full, locked file...) must not take the server down;
                    // try again a bit later rather than every minute.
                    logger.LogError(ex, "Scheduled backup failed; retrying in {Minutes} minutes.", RetryAfterFailure.TotalMinutes);
                    retryAt = DateTime.UtcNow + RetryAfterFailure;
                }
            }
            while (await WaitForNextTick(timer, stoppingToken));
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
