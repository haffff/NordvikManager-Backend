using NordvikManager.Infrastructure.Services.Backup;
using System.IO.Compression;
using System.Text.Json;

namespace NordvikManager.Application.UnitTests.Infrastructure.Backup
{
    public class BackupServiceTests : IDisposable
    {
        private readonly BackupTestEnvironment _env = new();

        public void Dispose() => _env.Dispose();

        private static ZipArchive OpenZip(BackupTestEnvironment env, string name) =>
            ZipFile.OpenRead(Path.Combine(env.Options.Folder, name));

        // =========================================================================
        // CreateAsync
        // =========================================================================

        // The server updater backs up before it swaps the program; a failed update restores it.
        [Fact]
        public async Task CreateAsync_PreUpdateBackup_IsListedWithItsKind()
        {
            var service = _env.CreateService();

            var info = await service.CreateAsync(BackupKind.PreUpdate, "to 0.3.0");

            Assert.Contains("-pre-update-", info.Name);
            Assert.Equal(BackupKind.PreUpdate, Assert.Single(service.List()).Kind);
        }

        [Fact]
        public async Task CreateAsync_WritesZipWithManifestDatabaseAndManagedFiles()
        {
            var gameId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();
            _env.AddGame(gameId, "Campaign");
            _env.AddManagedFile(gameId, resourceId, "image-bytes");

            var info = await _env.CreateService().CreateAsync(BackupKind.Manual, "before session");

            Assert.Equal("nordvik-backup-20261012-083000000-manual-before-session.zip", info.Name);
            Assert.Equal(BackupKind.Manual, info.Kind);
            Assert.Equal("before-session", info.Label);
            using var zip = OpenZip(_env, info.Name);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("manifest.json", names);
            Assert.Contains("db/data.db", names);
            Assert.Contains($"storage/games/{gameId}/{resourceId}", names);

            var manifest = JsonSerializer.Deserialize<BackupManifest>(zip.GetEntry("manifest.json")!.Open())!;
            Assert.Equal(BackupManifest.CurrentFormatVersion, manifest.FormatVersion);
            Assert.Equal("sqlite", manifest.Provider);
            Assert.Equal(BackupTestEnvironment.Migration, manifest.LatestMigrationId);
            Assert.Equal("manual", manifest.Kind);
            Assert.Equal(1, manifest.Games);
            Assert.Equal(1, manifest.Resources);
            Assert.Equal(1, manifest.Files);
        }

        [Fact]
        public async Task CreateAsync_SnapshotContainsTheDatabaseRows()
        {
            var gameId = Guid.NewGuid();
            _env.AddGame(gameId, "Campaign");

            var info = await _env.CreateService().CreateAsync(BackupKind.Manual, null);

            var extracted = Path.Combine(_env.Root, "extracted.db");
            using (var zip = OpenZip(_env, info.Name))
                zip.GetEntry("db/data.db")!.ExtractToFile(extracted);
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={extracted};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Name FROM Games";
            Assert.Equal("Campaign", command.ExecuteScalar());
        }

        [Fact]
        public async Task CreateAsync_ListsLinkedResourcesButDoesNotCopyThem()
        {
            var gameId = Guid.NewGuid();
            var linkedFile = Path.Combine(_env.Root, "elsewhere", "map.png");
            Directory.CreateDirectory(Path.GetDirectoryName(linkedFile)!);
            File.WriteAllText(linkedFile, "png");
            _env.AddGame(gameId, "Campaign");
            _env.AddLinked(gameId, Guid.NewGuid(), linkedFile);

            var info = await _env.CreateService().CreateAsync(BackupKind.Manual, null);

            using var zip = OpenZip(_env, info.Name);
            Assert.DoesNotContain(zip.Entries, e => e.FullName.EndsWith("map.png"));
            var manifest = JsonSerializer.Deserialize<BackupManifest>(zip.GetEntry("manifest.json")!.Open())!;
            var linked = Assert.Single(manifest.LinkedResources);
            Assert.Equal(linkedFile, linked.Path);
        }

        [Fact]
        public async Task CreateAsync_Throws_WhenProviderIsNotSupported()
        {
            _env.Options.IsSupported = false;

            await Assert.ThrowsAsync<BackupNotSupportedException>(() => _env.CreateService().CreateAsync(BackupKind.Manual, null));
        }

        // =========================================================================
        // List / Delete / OpenRead
        // =========================================================================

        [Fact]
        public async Task List_ReturnsBackupsNewestFirst_AndIgnoresOtherFiles()
        {
            var service = _env.CreateService();
            await service.CreateAsync(BackupKind.Manual, null);
            _env.Time.Advance(TimeSpan.FromHours(1));
            await service.CreateAsync(BackupKind.Scheduled, null);
            File.WriteAllText(Path.Combine(_env.Options.Folder, "notes.txt"), "x");

            var list = service.List();

            Assert.Equal(new[] { BackupKind.Scheduled, BackupKind.Manual }, list.Select(b => b.Kind));
            Assert.Equal(new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Utc), list[0].CreatedAtUtc);
            Assert.All(list, b => Assert.True(b.SizeBytes > 0));
        }

        [Theory]
        [InlineData("../data.db")]
        [InlineData("..\\data.db")]
        [InlineData("sub/nordvik-backup-20261012-083000000-manual.zip")]
        [InlineData("not-a-backup.zip")]
        public void Delete_RejectsNamesOutsideTheBackupFolder(string name)
        {
            Assert.Throws<BackupException>(() => _env.CreateService().Delete(name));
            Assert.True(File.Exists(_env.Options.DatabasePath));
        }

        [Fact]
        public async Task Delete_RemovesTheBackup()
        {
            var service = _env.CreateService();
            var info = await service.CreateAsync(BackupKind.Manual, null);

            service.Delete(info.Name);

            Assert.Empty(service.List());
        }

        [Fact]
        public void OpenRead_Throws_WhenBackupDoesNotExist()
        {
            Assert.Throws<BackupNotFoundException>(() => _env.CreateService().OpenRead("nordvik-backup-20261012-083000000-manual.zip"));
        }

        // =========================================================================
        // PruneScheduled
        // =========================================================================

        [Fact]
        public async Task PruneScheduled_KeepsNewestScheduled_AndNeverTouchesOtherKinds()
        {
            var service = _env.CreateService();
            for (var i = 0; i < 4; i++)
            {
                await service.CreateAsync(BackupKind.Scheduled, null);
                _env.Time.Advance(TimeSpan.FromHours(1));
            }
            await service.CreateAsync(BackupKind.Manual, null);
            _env.Time.Advance(TimeSpan.FromHours(1));
            await service.CreateAsync(BackupKind.PreRestore, null);

            var removed = service.PruneScheduled(2);

            Assert.Equal(2, removed);
            var list = service.List();
            Assert.Equal(2, list.Count(b => b.Kind == BackupKind.Scheduled));
            Assert.Single(list, b => b.Kind == BackupKind.Manual);
            Assert.Single(list, b => b.Kind == BackupKind.PreRestore);
            // Scheduled ones were made at 8:30, 9:30, 10:30 and 11:30 — the two oldest go.
            Assert.Equal(new DateTime(2026, 10, 12, 10, 30, 0, DateTimeKind.Utc), list.Where(b => b.Kind == BackupKind.Scheduled).Min(b => b.CreatedAtUtc));
        }

        // =========================================================================
        // Import / StageRestore / CancelPendingRestore
        // =========================================================================

        [Fact]
        public async Task StageRestoreAsync_TakesSafetyBackup_ExtractsToStaging_AndWritesMarker()
        {
            var gameId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();
            _env.AddGame(gameId, "Campaign");
            _env.AddManagedFile(gameId, resourceId, "image-bytes");
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            _env.Time.Advance(TimeSpan.FromMinutes(5));

            var pending = await service.StageRestoreAsync(backup.Name);

            Assert.Equal(backup.Name, pending.BackupName);
            Assert.Equal(pending.BackupName, service.GetPendingRestore()?.BackupName);
            Assert.Single(service.List(), b => b.Kind == BackupKind.PreRestore);
            Assert.True(File.Exists(Path.Combine(_env.Options.StagingFolder, "data.db")));
            Assert.True(File.Exists(Path.Combine(_env.Options.StagingFolder, "storage", "games", gameId.ToString(), resourceId.ToString())));
        }

        [Fact]
        public async Task CancelPendingRestore_RemovesMarkerAndStaging()
        {
            var service = _env.CreateService();
            var backup = await service.CreateAsync(BackupKind.Manual, null);
            await service.StageRestoreAsync(backup.Name);

            service.CancelPendingRestore();

            Assert.Null(service.GetPendingRestore());
            Assert.False(Directory.Exists(_env.Options.StagingFolder));
        }

        [Fact]
        public async Task ImportAsync_StoresAValidArchiveAsAnImportedBackup()
        {
            var service = _env.CreateService();
            var source = await service.CreateAsync(BackupKind.Manual, null);
            var bytes = File.ReadAllBytes(Path.Combine(_env.Options.Folder, source.Name));
            _env.Time.Advance(TimeSpan.FromMinutes(1));

            var imported = await service.ImportAsync(new MemoryStream(bytes));

            Assert.Equal(BackupKind.Imported, imported.Kind);
            Assert.Equal(2, service.List().Count);
        }

        [Fact]
        public async Task ImportAsync_RejectsGarbage_AndLeavesNoFileBehind()
        {
            var service = _env.CreateService();

            await Assert.ThrowsAsync<BackupException>(() => service.ImportAsync(new MemoryStream(new byte[] { 1, 2, 3 })));

            Assert.Empty(service.List());
            Assert.Empty(Directory.GetFiles(_env.Options.Folder));
        }
    }
}
