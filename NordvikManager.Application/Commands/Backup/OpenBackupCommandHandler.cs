using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Backup
{
    internal class OpenBackupCommandHandler : HandlerBase<OpenBackupCommand, Stream>
    {
        private readonly IBackupService backups;

        public OpenBackupCommandHandler(IDbContext dbContext, IMapper mapper, IBackupService backups) : base(dbContext, mapper)
        {
            this.backups = backups;
        }

        public override Task<Stream> Handle(OpenBackupCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(backups.OpenRead(request.Name));
    }
}
