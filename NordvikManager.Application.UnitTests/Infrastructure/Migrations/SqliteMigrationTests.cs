using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NordvikManager.Data.Contexts;
using NordvikManager.Domain.Entities;
using Xunit;

namespace NordvikManager.Application.UnitTests.Infrastructure.Migrations
{
    /// <summary>
    /// Every migration runs in one transaction. A step EF has to run outside it (on SQLite,
    /// the PRAGMA around a table rebuild) splits a migration in parts: if the server or the
    /// browser tab stops in between, the schema has changed but the migration isn't recorded,
    /// and the next start runs it again and fails.
    /// </summary>
    public class SqliteMigrationTests
    {
        private static DndOneContext Context(SqliteConnection connection) => new(
            new DbContextOptionsBuilder<DndOneContext>()
                .UseSqlite(connection)
                .ConfigureWarnings(w => w.Throw(RelationalEventId.NonTransactionalMigrationOperationWarning))
                .Options);

        [Fact]
        public void EveryMigration_RunsInsideItsTransaction()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var db = Context(connection);

            db.Database.Migrate();

            Assert.Empty(db.Database.GetPendingMigrations());
        }

        [Fact]
        public void AddonDependencies_KeepsOneCopyPerGame_AndTheOldParentColumnIsInert()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var db = Context(connection);
            db.Database.Migrate();

            using var columns = connection.CreateCommand();
            columns.CommandText = "SELECT name FROM pragma_table_info('Addons')";
            var names = new List<string>();
            using (var reader = columns.ExecuteReader())
                while (reader.Read()) names.Add(reader.GetString(0));
            Assert.Contains("Dependencies", names);

            using var indexes = connection.CreateCommand();
            indexes.CommandText = "SELECT name FROM pragma_index_list('Addons')";
            var indexNames = new List<string>();
            using (var reader = indexes.ExecuteReader())
                while (reader.Read()) indexNames.Add(reader.GetString(0));
            Assert.Contains("IX_Addons_GameModelId_Key", indexNames);
            Assert.DoesNotContain("IX_Addons_AddonModelId", indexNames);
            Assert.DoesNotContain("IX_Addons_GameModelId", indexNames);
        }

        [Fact]
        public void AnOlderDatabase_Upgrades_AndItsAddonsCanStillBeDeleted()
        {
            // A 0.0.2 database: one addon pointing at another through the old parent column.
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var db = Context(connection);
            db.GetService<IMigrator>().Migrate("20261027225126_SoundboardLoops");
            var parent = Guid.NewGuid();
            var child = Guid.NewGuid();
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Addons (Id, Name, Key, IsEnabled, InstallHookFired, AddonModelId) VALUES " +
                $"('{parent.ToString().ToUpperInvariant()}', 'Parent', 'parent', 1, 1, NULL), " +
                $"('{child.ToString().ToUpperInvariant()}', 'Child', 'child', 1, 1, '{parent.ToString().ToUpperInvariant()}');");

            db.Database.Migrate();

            // The leftover column no longer ties them: the parent can go first.
            db.Set<AddonModel>().Remove(db.Set<AddonModel>().Single(a => a.Id == parent));
            db.SaveChanges();
            db.Set<AddonModel>().Add(new AddonModel { Id = Guid.NewGuid(), Name = "New", Key = "new" });
            db.SaveChanges();
            Assert.Equal(new[] { "child", "new" }, db.Set<AddonModel>().Select(a => a.Key).OrderBy(k => k).ToArray());
        }
    }
}
