using NordvikManager.Infrastructure.Services.Backup;
using Microsoft.Data.Sqlite;

namespace NordvikManager.Application.UnitTests.Infrastructure.Backup
{
    /// <summary>
    /// A throwaway server layout on disk: a SQLite data.db with just the tables the backup
    /// code reads (Games, Resources, __EFMigrationsHistory), a managed-file storage dir and a
    /// backups folder — all under one temp root that is deleted on dispose.
    /// </summary>
    internal sealed class BackupTestEnvironment : IDisposable
    {
        public const string Migration = "20261007225115_InitialCreate";

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "nordvik-backup-tests-" + Guid.NewGuid());
        public BackupOptions Options { get; }
        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 12, 8, 30, 0, TimeSpan.Zero));

        public BackupTestEnvironment()
        {
            Directory.CreateDirectory(Root);
            Options = new BackupOptions
            {
                Folder = Path.Combine(Root, "backups"),
                StagingFolder = Path.Combine(Root, "backups", ".staging"),
                DatabasePath = Path.Combine(Root, "data.db"),
                StorageBasePath = Path.Combine(Root, "storage"),
                IsSupported = true,
            };
            CreateSchema();
        }

        public BackupService CreateService(IReadOnlyCollection<string>? knownMigrations = null) =>
            new(Options, Time, knownMigrations ?? new[] { Migration });

        public SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={Options.DatabasePath};Pooling=False");
            connection.Open();
            return connection;
        }

        public void Exec(string sql)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public object? Scalar(string sql)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }

        private void CreateSchema() => Exec($@"
            CREATE TABLE Games (Id TEXT NOT NULL PRIMARY KEY, Name TEXT);
            CREATE TABLE Resources (Id TEXT NOT NULL PRIMARY KEY, GameId TEXT NOT NULL, Storage INTEGER NOT NULL, Path TEXT);
            CREATE TABLE __EFMigrationsHistory (MigrationId TEXT NOT NULL PRIMARY KEY, ProductVersion TEXT NOT NULL);
            INSERT INTO __EFMigrationsHistory VALUES ('{Migration}', '10.0.5');");

        public void AddGame(Guid id, string name) => Exec($"INSERT INTO Games VALUES ('{id.ToString().ToUpperInvariant()}', '{name}')");

        /// <summary>Adds a ManagedFile resource the way LocalFileStorageProvider stores one: bytes on disk, absolute Path in the row.</summary>
        public string AddManagedFile(Guid gameId, Guid resourceId, string content)
        {
            var dir = Path.Combine(Options.StorageBasePath, "games", gameId.ToString());
            Directory.CreateDirectory(dir);
            var path = Path.GetFullPath(Path.Combine(dir, resourceId.ToString()));
            File.WriteAllText(path, content);
            Exec($"INSERT INTO Resources VALUES ('{resourceId.ToString().ToUpperInvariant()}', '{gameId.ToString().ToUpperInvariant()}', 1, '{path}')");
            return path;
        }

        public void AddLinked(Guid gameId, Guid resourceId, string path) =>
            Exec($"INSERT INTO Resources VALUES ('{resourceId.ToString().ToUpperInvariant()}', '{gameId.ToString().ToUpperInvariant()}', 2, '{path}')");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    internal sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public FakeTimeProvider(DateTimeOffset now) => Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now = Now.Add(by);
    }
}
