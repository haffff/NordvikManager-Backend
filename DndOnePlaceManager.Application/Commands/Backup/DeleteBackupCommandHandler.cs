using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Backup
{
    internal class DeleteBackupCommandHandler : HandlerBase<DeleteBackupCommand, bool>
    {
        private readonly IBackupService backups;

        public DeleteBackupCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override Task<bool> Handle(DeleteBackupCommand request, CancellationToken cancellationToken)
        {
            backups.Delete(request.Name);
            return Task.FromResult(true);
        }
    }
}
