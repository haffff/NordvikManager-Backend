using Microsoft.Extensions.Configuration;
using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Application.UnitTests.Infrastructure.Backup
{
    // The released server keeps its database in data/data.db. SQLite won't create a missing
    // folder, so on a fresh unzip the first start failed opening the database.
    public class BackupOptionsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nm-backup-options-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        private static BackupOptions Options(bool useSqlite, string connectionString) =>
            BackupOptions.FromConfiguration(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseSqlite"] = useSqlite.ToString(),
                    ["ConnectionStrings:DBData"] = connectionString,
                })
                .Build());

        [Fact]
        public void EnsureDatabaseFolder_CreatesMissingFolder_ForSqlite()
        {
            var databaseFolder = Path.Combine(_folder, "data");
            var options = Options(useSqlite: true, $"Data Source={Path.Combine(databaseFolder, "data.db")}");

            options.EnsureDatabaseFolder();

            Assert.True(Directory.Exists(databaseFolder));
        }

        [Fact]
        public void EnsureDatabaseFolder_DoesNothing_WithoutADatabaseFile()
        {
            var options = Options(useSqlite: false, "Server=localhost;Database=nordvik");

            options.EnsureDatabaseFolder();

            Assert.Equal("", options.DatabasePath);
        }
    }
}
