using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    internal class CreateBackupCommandHandler : HandlerBase<CreateBackupCommand, BackupInfo>
    {
        private readonly IBackupService backups;

        public CreateBackupCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override Task<BackupInfo> Handle(CreateBackupCommand request, CancellationToken cancellationToken) =>
            backups.CreateAsync(BackupKind.Manual, request.Label, cancellationToken);
    }
}
