using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    public class CreateBackupCommand : CommandBase<BackupInfo>
    {
        public string? Label { get; set; }
    }
}
