using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Application.DataTransferObjects.Backup;
using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.Commands.Backup
{
    internal class SetBackupScheduleCommandHandler : HandlerBase<SetBackupScheduleCommand, BackupScheduleDTO>
    {
        private readonly IBackupScheduleStore schedules;

        public SetBackupScheduleCommandHandler(IDbContext dbContext, IMapper mapper, IBackupScheduleStore schedules) : base(dbContext, mapper)
        {
            this.schedules = schedules;
        }

        public const int MaxIntervalHours = 24 * 365;
        public const int MaxRetentionCount = 1000;

        public override Task<BackupScheduleDTO> Handle(SetBackupScheduleCommand request, CancellationToken cancellationToken)
        {
            if (request.IntervalHours < 1 || request.IntervalHours > MaxIntervalHours)
                throw new BackupException($"Backup interval must be between 1 and {MaxIntervalHours} hours.");
            if (request.RetentionCount < 1 || request.RetentionCount > MaxRetentionCount)
                throw new BackupException($"Number of scheduled backups to keep must be between 1 and {MaxRetentionCount}.");

            // LastRunUtc is kept: changing the interval moves the next run, it doesn't trigger one.
            var schedule = schedules.Load();
            schedule.Enabled = request.Enabled;
            schedule.IntervalHours = request.IntervalHours;
            schedule.RetentionCount = request.RetentionCount;
            schedules.Save(schedule);

            return Task.FromResult(BackupScheduleDTO.From(schedule));
        }
    }
}
