using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    public class StageRestoreCommand : CommandBase<PendingRestoreInfo>
    {
        // Either the name of a backup already in the backup folder...
        public string? Name { get; set; }
        // ...or an uploaded backup zip, which is imported first.
        public Stream? Upload { get; set; }
    }
}
