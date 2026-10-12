using DndOnePlaceManager.Infrastructure.Services.Backup;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Backup
{
    public class PendingRestoreApplierTests : IDisposable
    {
        private readonly BackupTestEnvironment _env = new();

        public void Dispose() => _env.Dispose();

        [Fact]
        public void Apply_DoesNothing_WithoutAMarker()
        {
            Assert.Equal(PendingRestoreResult.None, PendingRestoreApplier.Apply(_env.Options));
        }

        [Fact]
        public async Task Apply_SwapsInTheStagedDatabaseAndFiles()
        {
            var gameId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();
            _env.AddGame(gameId, "Before");
            _env.AddManagedFile(gameId, resourceId, "old-bytes");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);

            // Changes made after the backup, which the restore should undo.
            _env.Exec("UPDATE Games SET Name = 'After'");
            var extraGame = Guid.NewGuid();
            _env.AddManagedFile(extraGame, Guid.NewGuid(), "new-bytes");

            await service.StageRestoreAsync(backup.Name);

            var result = PendingRestoreApplier.Apply(_env.Options);

            Assert.Equal(PendingRestoreResult.Applied, result);
            Assert.Equal("Before", _env.Scalar("SELECT Name FROM Games"));
            Assert.True(File.Exists(Path.Combine(_env.Options.StorageBasePath, "games", gameId.ToString(), resourceId.ToString())));
            Assert.False(Directory.Exists(Path.Combine(_env.Options.StorageBasePath, "games", extraGame.ToString())));
            Assert.Null(service.GetPendingRestore());
            Assert.False(Directory.Exists(_env.Options.StagingFolder));
        }

        [Fact]
        public async Task Apply_RewritesManagedFilePathsToTheCurrentStorageFolder()
        {
            var gameId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();
            _env.AddGame(gameId, "Campaign");
            _env.AddManagedFile(gameId, resourceId, "bytes");
            // Pretend the backup came from another machine with another storage location.
            _env.Exec($"UPDATE Resources SET Path = 'D:\\old-server\\data\\storage\\games\\{gameId}\\{resourceId}'");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            await service.StageRestoreAsync(backup.Name);

            var messages = new List<string>();
            var result = PendingRestoreApplier.Apply(_env.Options, messages.Add);
            Assert.True(result == PendingRestoreResult.Applied, string.Join("; ", messages));

            var expected = Path.GetFullPath(Path.Combine(_env.Options.StorageBasePath, "games", gameId.ToString(), resourceId.ToString()));
            Assert.Equal(expected, _env.Scalar("SELECT Path FROM Resources"));
        }

        [Fact]
        public async Task Apply_LeavesLinkedPathsAlone()
        {
            var gameId = Guid.NewGuid();
            _env.AddGame(gameId, "Campaign");
            _env.AddLinked(gameId, Guid.NewGuid(), "C:\\maps\\map.png");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            await service.StageRestoreAsync(backup.Name);

            PendingRestoreApplier.Apply(_env.Options);

            Assert.Equal("C:\\maps\\map.png", _env.Scalar("SELECT Path FROM Resources"));
        }

        [Fact]
        public async Task Apply_RollsBack_AndMarksFailed_WhenTheStagedDatabaseIsMissing()
        {
            _env.AddGame(Guid.NewGuid(), "Current");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            await service.StageRestoreAsync(backup.Name);
            File.Delete(Path.Combine(_env.Options.StagingFolder, "data.db"));

            var result = PendingRestoreApplier.Apply(_env.Options);

            Assert.Equal(PendingRestoreResult.Failed, result);
            Assert.Equal("Current", _env.Scalar("SELECT Name FROM Games"));
            Assert.Null(service.GetPendingRestore());
            Assert.Equal(backup.Name, service.GetFailedRestore()?.BackupName);
        }

        [Fact]
        public async Task Apply_RollsBack_WhenTheStagedDatabaseIsCorrupt()
        {
            _env.AddGame(Guid.NewGuid(), "Current");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            await service.StageRestoreAsync(backup.Name);
            File.WriteAllText(Path.Combine(_env.Options.StagingFolder, "data.db"), "this is not a sqlite file");

            var result = PendingRestoreApplier.Apply(_env.Options);

            Assert.Equal(PendingRestoreResult.Failed, result);
            Assert.Equal("Current", _env.Scalar("SELECT Name FROM Games"));
        }
    }
}
