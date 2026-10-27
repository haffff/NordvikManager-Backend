using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    internal class StageRestoreCommandHandler : HandlerBase<StageRestoreCommand, PendingRestoreInfo>
    {
        private readonly IBackupService backups;

        public StageRestoreCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override async Task<PendingRestoreInfo> Handle(StageRestoreCommand request, CancellationToken cancellationToken)
        {
            var name = request.Name;
            if (request.Upload != null)
                name = (await backups.ImportAsync(request.Upload, cancellationToken)).Name;
            if (string.IsNullOrEmpty(name))
                throw new BackupException("Choose a backup to restore.");

            return await backups.StageRestoreAsync(name, cancellationToken);
        }
    }
}
