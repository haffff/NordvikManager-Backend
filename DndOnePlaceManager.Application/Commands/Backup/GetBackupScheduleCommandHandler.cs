using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Application.DataTransferObjects.Backup;

namespace DndOnePlaceManager.Application.Commands.Backup
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
