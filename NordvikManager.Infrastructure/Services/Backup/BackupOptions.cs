using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace NordvikManager.Infrastructure.Services.Backup
{
    /// <summary>Where the backup code finds the live data and keeps its own files. All paths absolute.</summary>
    public class BackupOptions
    {
        public string Folder { get; set; } = "";
        public string StagingFolder { get; set; } = "";
        public string DatabasePath { get; set; } = "";
        // FileStorage:BasePath — managed resource files live under {StorageBasePath}/games.
        public string StorageBasePath { get; set; } = "";
        // Only the SQLite database can be snapshotted and swapped as a file.
        public bool IsSupported { get; set; }

        public string GamesStoragePath => Path.Combine(StorageBasePath, "games");
        public string PendingMarkerPath => Path.Combine(Folder, "pending-restore.json");
        public string FailedMarkerPath => Path.Combine(Folder, "pending-restore.failed.json");
        public string ScheduleFilePath => Path.Combine(Folder, "schedule.json");

        /// <summary>SQLite won't create a missing folder for its file (e.g. data/ on a fresh install).</summary>
        public void EnsureDatabaseFolder()
        {
            if (DatabasePath != "")
                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        }

        public static BackupOptions FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("Backup");
            var folder = Path.GetFullPath(section["Folder"] ?? "./data/backups");
            var useSqlite = configuration.GetValue<bool>("UseSqlite");
            var connectionString = configuration.GetSection("ConnectionStrings")["DBData"];

            var databasePath = "";
            if (useSqlite && !string.IsNullOrWhiteSpace(connectionString))
            {
                var builder = new SqliteConnectionStringBuilder(connectionString);
                if (builder.Mode != SqliteOpenMode.Memory && builder.DataSource is not ("" or ":memory:"))
                    databasePath = Path.GetFullPath(builder.DataSource);
            }

            return new BackupOptions
            {
                Folder = folder,
                StagingFolder = Path.GetFullPath(section["StagingFolder"] ?? Path.Combine(folder, ".staging")),
                DatabasePath = databasePath,
                StorageBasePath = Path.GetFullPath(configuration.GetSection("FileStorage")["BasePath"] ?? "./data/storage"),
                IsSupported = databasePath != "",
            };
        }
    }
}
