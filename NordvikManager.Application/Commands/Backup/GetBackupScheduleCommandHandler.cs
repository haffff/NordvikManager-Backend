using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Application.DataTransferObjects.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    internal class GetBackupScheduleCommandHandler : HandlerBase<GetBackupScheduleCommand, BackupScheduleDTO>
    {
        private readonly IBackupScheduleStore schedules;

        public GetBackupScheduleCommandHandler(IDbContext dbContext, IMapper mapper, IBackupScheduleStore schedules) : base(dbContext, mapper)
        {
            this.schedules = schedules;
        }

        public override Task<BackupScheduleDTO> Handle(GetBackupScheduleCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(BackupScheduleDTO.From(schedules.Load()));
    }
}
