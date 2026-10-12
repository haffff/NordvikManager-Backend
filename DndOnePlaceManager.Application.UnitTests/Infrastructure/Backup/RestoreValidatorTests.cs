using DndOnePlaceManager.Infrastructure.Services.Backup;
using System.IO.Compression;
using System.Text.Json;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Backup
{
    public class RestoreValidatorTests
    {
        private static readonly string[] Known = { "20261007225115_InitialCreate", "20261101000000_Later" };

        private static ZipArchive Zip(BackupManifest? manifest, bool withDb = true, string? rawManifest = null)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (manifest != null || rawManifest != null)
                {
                    using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
                    writer.Write(rawManifest ?? JsonSerializer.Serialize(manifest));
                }
                if (withDb)
                {
                    using var writer = new StreamWriter(archive.CreateEntry("db/data.db").Open());
                    writer.Write("db");
                }
            }
            stream.Position = 0;
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }

        private static BackupManifest Valid() => new()
        {
            FormatVersion = BackupManifest.CurrentFormatVersion,
            Provider = "sqlite",
            LatestMigrationId = "20261007225115_InitialCreate",
            Kind = "manual",
        };

        [Fact]
        public void Validate_ReturnsManifest_ForAValidArchive()
        {
            using var zip = Zip(Valid());

            var manifest = RestoreValidator.Validate(zip, Known);

            Assert.Equal("sqlite", manifest.Provider);
        }

        [Fact]
        public void Validate_AcceptsAnOlderSchema()
        {
            // An older backup is migrated forward by Database.Migrate() at the next startup.
            using var zip = Zip(Valid());

            RestoreValidator.Validate(zip, Known);
        }

        [Fact]
        public void Validate_Rejects_WhenSchemaIsNewerThanThisApp()
        {
            var manifest = Valid();
            manifest.LatestMigrationId = "20270101000000_FromTheFuture";
            using var zip = Zip(manifest);

            var ex = Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
            Assert.Contains("newer", ex.Message);
        }

        [Fact]
        public void Validate_Rejects_WrongProvider()
        {
            var manifest = Valid();
            manifest.Provider = "mysql";
            using var zip = Zip(manifest);

            Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
        }

        [Fact]
        public void Validate_Rejects_UnknownFormatVersion()
        {
            var manifest = Valid();
            manifest.FormatVersion = BackupManifest.CurrentFormatVersion + 1;
            using var zip = Zip(manifest);

            Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
        }

        [Fact]
        public void Validate_Rejects_MissingManifest()
        {
            using var zip = Zip(null);

            Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
        }

        [Fact]
        public void Validate_Rejects_BrokenManifest()
        {
            using var zip = Zip(null, rawManifest: "{ not json");

            Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
        }

        [Fact]
        public void Validate_Rejects_MissingDatabase()
        {
            using var zip = Zip(Valid(), withDb: false);

            Assert.Throws<BackupException>(() => RestoreValidator.Validate(zip, Known));
        }

        [Fact]
        public void KnownMigrations_IncludesTheInitialMigration()
        {
            Assert.Contains("20261007225115_InitialCreate", RestoreValidator.KnownMigrations());
        }
    }
}
