using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.DataTransferObjects.Backup
{
    public class BackupStatusDTO
    {
        // False on MySQL — the backup endpoints then answer 501.
        public bool Supported { get; set; }
        public IReadOnlyList<BackupInfo> Backups { get; set; } = Array.Empty<BackupInfo>();
        public PendingRestoreInfo? PendingRestore { get; set; }
        public PendingRestoreInfo? FailedRestore { get; set; }
    }

    public class BackupScheduleDTO
    {
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; }
        public int RetentionCount { get; set; }
        public DateTime? LastRunUtc { get; set; }
        public DateTime? NextRunUtc { get; set; }

        public static BackupScheduleDTO From(BackupSchedule schedule) => new()
        {
            Enabled = schedule.Enabled,
            IntervalHours = schedule.IntervalHours,
            RetentionCount = schedule.RetentionCount,
            LastRunUtc = schedule.LastRunUtc,
            NextRunUtc = schedule.NextRunUtc,
        };
    }
}
