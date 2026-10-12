using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Application.DataTransferObjects.Backup;

namespace DndOnePlaceManager.Application.Commands.Backup
{
    internal class GetBackupsCommandHandler : HandlerBase<GetBackupsCommand, BackupStatusDTO>
    {
        private readonly IBackupService backups;

        public GetBackupsCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override Task<BackupStatusDTO> Handle(GetBackupsCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(new BackupStatusDTO
            {
                Supported = backups.IsSupported,
                Backups = backups.List(),
                PendingRestore = backups.GetPendingRestore(),
                FailedRestore = backups.GetFailedRestore(),
            });
    }
}
