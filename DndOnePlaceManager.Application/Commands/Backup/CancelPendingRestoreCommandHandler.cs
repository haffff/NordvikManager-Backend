using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Backup
{
    internal class CancelPendingRestoreCommandHandler : HandlerBase<CancelPendingRestoreCommand, bool>
    {
        private readonly IBackupService backups;

        public CancelPendingRestoreCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override Task<bool> Handle(CancelPendingRestoreCommand request, CancellationToken cancellationToken)
        {
            backups.CancelPendingRestore();
            return Task.FromResult(true);
        }
    }
}
