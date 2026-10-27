using NordvikManager.Application.Commands.Backup;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Backup;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Backup
{
    public class BackupCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IBackupService> _backups = new();
        private readonly Mock<IBackupScheduleStore> _schedule = new();

        public BackupCommandHandlerTests()
        {
            _backups.SetupGet(b => b.IsSupported).Returns(true);
            _schedule.Setup(s => s.Load()).Returns(new BackupSchedule());
        }

        // =========================================================================
        // SetBackupSchedule
        // =========================================================================

        [Fact]
        public async Task SetBackupSchedule_SavesSettings_AndKeepsLastRun()
        {
            var lastRun = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            _schedule.Setup(s => s.Load()).Returns(new BackupSchedule { LastRunUtc = lastRun });
            BackupSchedule? saved = null;
            _schedule.Setup(s => s.Save(It.IsAny<BackupSchedule>())).Callback<BackupSchedule>(s => saved = s);
            var handler = new SetBackupScheduleCommandHandler(Db, Mapper, _schedule.Object);

            var result = await handler.Handle(new SetBackupScheduleCommand { Enabled = true, IntervalHours = 12, RetentionCount = 3 }, default);

            Assert.NotNull(saved);
            Assert.True(saved!.Enabled);
            Assert.Equal(12, saved.IntervalHours);
            Assert.Equal(3, saved.RetentionCount);
            Assert.Equal(lastRun, saved.LastRunUtc);
            Assert.Equal(lastRun.AddHours(12), result.NextRunUtc);
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(24 * 366, 3)]
        [InlineData(24, 0)]
        [InlineData(24, 1001)]
        public async Task SetBackupSchedule_Throws_ForOutOfRangeValues(int intervalHours, int retention)
        {
            var handler = new SetBackupScheduleCommandHandler(Db, Mapper, _schedule.Object);

            await Assert.ThrowsAsync<BackupException>(() =>
                handler.Handle(new SetBackupScheduleCommand { Enabled = true, IntervalHours = intervalHours, RetentionCount = retention }, default));
            _schedule.Verify(s => s.Save(It.IsAny<BackupSchedule>()), Times.Never);
        }

        // =========================================================================
        // StageRestore
        // =========================================================================

        [Fact]
        public async Task StageRestore_FromUpload_ImportsThenStagesTheImportedBackup()
        {
            var upload = new MemoryStream(new byte[] { 1 });
            var imported = new BackupInfo("nordvik-backup-20261012-083000000-imported.zip", BackupKind.Imported, DateTime.UtcNow, 1, null);
            _backups.Setup(b => b.ImportAsync(upload, It.IsAny<CancellationToken>())).ReturnsAsync(imported);
            _backups.Setup(b => b.StageRestoreAsync(imported.Name, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PendingRestoreInfo(imported.Name, DateTime.UtcNow));
            var handler = new StageRestoreCommandHandler(Db, Mapper, _backups.Object);

            var pending = await handler.Handle(new StageRestoreCommand { Upload = upload }, default);

            Assert.Equal(imported.Name, pending.BackupName);
        }

        [Fact]
        public async Task StageRestore_ByName_StagesThatBackup()
        {
            const string name = "nordvik-backup-20261012-083000000-manual.zip";
            _backups.Setup(b => b.StageRestoreAsync(name, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PendingRestoreInfo(name, DateTime.UtcNow));
            var handler = new StageRestoreCommandHandler(Db, Mapper, _backups.Object);

            var pending = await handler.Handle(new StageRestoreCommand { Name = name }, default);

            Assert.Equal(name, pending.BackupName);
            _backups.Verify(b => b.ImportAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // =========================================================================
        // GetBackups
        // =========================================================================

        [Fact]
        public async Task GetBackups_ReturnsListPendingAndFailedRestore()
        {
            var backup = new BackupInfo("nordvik-backup-20261012-083000000-manual.zip", BackupKind.Manual, DateTime.UtcNow, 10, null);
            _backups.Setup(b => b.List()).Returns(new[] { backup });
            _backups.Setup(b => b.GetPendingRestore()).Returns(new PendingRestoreInfo(backup.Name, DateTime.UtcNow));
            var handler = new GetBackupsCommandHandler(Db, Mapper, _backups.Object);

            var result = await handler.Handle(new GetBackupsCommand(), default);

            Assert.True(result.Supported);
            Assert.Single(result.Backups);
            Assert.Equal(backup.Name, result.PendingRestore?.BackupName);
            Assert.Null(result.FailedRestore);
        }
    }
}
