using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.UnitTests.Infrastructure.Backup
{
    public class ScheduledBackupRunnerTests : IDisposable
    {
        private readonly BackupTestEnvironment _env = new();

        public void Dispose() => _env.Dispose();

        private (ScheduledBackupRunner runner, BackupService service, BackupScheduleStore store) Create()
        {
            var service = _env.CreateService();
            var store = new BackupScheduleStore(_env.Options);
            return (new ScheduledBackupRunner(service, store, _env.Time), service, store);
        }

        [Fact]
        public void ScheduleStore_ReturnsDisabledDefaults_WhenNothingSaved()
        {
            var schedule = new BackupScheduleStore(_env.Options).Load();

            Assert.False(schedule.Enabled);
            Assert.Equal(24, schedule.IntervalHours);
            Assert.Equal(7, schedule.RetentionCount);
            Assert.Null(schedule.LastRunUtc);
        }

        [Fact]
        public void ScheduleStore_RoundTrips()
        {
            var store = new BackupScheduleStore(_env.Options);
            var last = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            store.Save(new BackupSchedule { Enabled = true, IntervalHours = 6, RetentionCount = 3, LastRunUtc = last });

            var loaded = new BackupScheduleStore(_env.Options).Load();
            Assert.True(loaded.Enabled);
            Assert.Equal(6, loaded.IntervalHours);
            Assert.Equal(3, loaded.RetentionCount);
            Assert.Equal(last, loaded.LastRunUtc);
        }

        [Fact]
        public async Task RunIfDueAsync_DoesNothing_WhenDisabled()
        {
            var (runner, service, _) = Create();

            Assert.False(await runner.RunIfDueAsync());
            Assert.Empty(service.List());
        }

        [Fact]
        public async Task RunIfDueAsync_RunsOnFirstTick_ThenWaitsForTheInterval()
        {
            var (runner, service, store) = Create();
            store.Save(new BackupSchedule { Enabled = true, IntervalHours = 6, RetentionCount = 5 });

            Assert.True(await runner.RunIfDueAsync());
            _env.Time.Advance(TimeSpan.FromHours(5));
            Assert.False(await runner.RunIfDueAsync());
            _env.Time.Advance(TimeSpan.FromHours(1));
            Assert.True(await runner.RunIfDueAsync());

            Assert.Equal(2, service.List().Count(b => b.Kind == BackupKind.Scheduled));
            Assert.Equal(_env.Time.Now.UtcDateTime, store.Load().LastRunUtc);
        }

        [Fact]
        public async Task RunIfDueAsync_PrunesToTheRetentionCount()
        {
            var (runner, service, store) = Create();
            store.Save(new BackupSchedule { Enabled = true, IntervalHours = 1, RetentionCount = 2 });

            for (var i = 0; i < 4; i++)
            {
                await runner.RunIfDueAsync();
                _env.Time.Advance(TimeSpan.FromHours(1));
            }

            Assert.Equal(2, service.List().Count(b => b.Kind == BackupKind.Scheduled));
        }

        [Fact]
        public void NextRunUtc_IsLastRunPlusInterval()
        {
            var schedule = new BackupSchedule { Enabled = true, IntervalHours = 12, LastRunUtc = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc) };

            Assert.Equal(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), schedule.NextRunUtc);
            Assert.Null(new BackupSchedule { Enabled = false }.NextRunUtc);
        }
    }
}
