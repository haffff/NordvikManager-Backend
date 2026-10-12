using DndOnePlaceManager.Application.DataTransferObjects.Backup;

namespace DndOnePlaceManager.Application.Commands.Backup
{
    public class SetBackupScheduleCommand : CommandBase<BackupScheduleDTO>
    {
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; }
        public int RetentionCount { get; set; }
    }
}
