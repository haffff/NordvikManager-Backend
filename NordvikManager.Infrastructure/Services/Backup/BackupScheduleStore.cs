using NordvikManager.Infrastructure.Interfaces;
using System.Text.Json;

namespace NordvikManager.Infrastructure.Services.Backup
{
    /// <summary>
    /// The backup schedule lives in a JSON file in the backup folder, not in the database —
    /// restoring a backup must not quietly switch the schedule back to what it was then.
    /// </summary>
    public class BackupScheduleStore : IBackupScheduleStore
    {
        private readonly BackupOptions options;
        private readonly object sync = new();

        public BackupScheduleStore(BackupOptions options)
        {
            this.options = options;
        }

        public BackupSchedule Load()
        {
            lock (sync)
            {
                if (!File.Exists(options.ScheduleFilePath))
                    return new BackupSchedule();
                try
                {
                    return JsonSerializer.Deserialize<BackupSchedule>(File.ReadAllText(options.ScheduleFilePath)) ?? new BackupSchedule();
                }
                catch (JsonException)
                {
                    return new BackupSchedule();
                }
            }
        }

        public void Save(BackupSchedule schedule)
        {
            lock (sync)
            {
                Directory.CreateDirectory(options.Folder);
                File.WriteAllText(options.ScheduleFilePath, JsonSerializer.Serialize(schedule, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    /// <summary>One tick of the backup schedule; the hosted service just calls this every minute.</summary>
    public class ScheduledBackupRunner
    {
        private readonly IBackupService backups;
        private readonly IBackupScheduleStore store;
        private readonly TimeProvider time;

        public ScheduledBackupRunner(IBackupService backups, IBackupScheduleStore store, TimeProvider time)
        {
            this.backups = backups;
            this.store = store;
            this.time = time;
        }

        /// <returns>True if a backup was made.</returns>
        public async Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default)
        {
            var schedule = store.Load();
            var now = time.GetUtcNow().UtcDateTime;
            if (!backups.IsSupported || !schedule.IsDue(now))
                return false;

            await backups.CreateAsync(BackupKind.Scheduled, null, cancellationToken);

            // Re-read: the GM may have changed the schedule while the backup ran.
            var latest = store.Load();
            latest.LastRunUtc = now;
            store.Save(latest);

            backups.PruneScheduled(latest.RetentionCount);
            return true;
        }
    }
}
