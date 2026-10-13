using DndOnePlaceManager.Infrastructure.Services.Backup;

namespace DndOnePlaceManager.Application.Commands.Backup
{
    public class CreateBackupCommand : CommandBase<BackupInfo>
    {
        public string? Label { get; set; }
    }
}
