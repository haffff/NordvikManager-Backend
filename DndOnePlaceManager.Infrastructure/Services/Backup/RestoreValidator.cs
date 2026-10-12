using DNDOnePlaceManager.Data.Contexts;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace DndOnePlaceManager.Infrastructure.Services.Backup
{
    public static class RestoreValidator
    {
        public const string ManifestEntry = "manifest.json";
        public const string DatabaseEntry = "db/data.db";
        public const string StoragePrefix = "storage/";

        /// <summary>
        /// Checks a backup zip can be restored by this app and returns its manifest. A backup
        /// from an older schema is fine (Database.Migrate() upgrades it at the next startup);
        /// one from a newer app is not — this app doesn't know its migrations.
        /// </summary>
        public static BackupManifest Validate(ZipArchive archive, IReadOnlyCollection<string> knownMigrations)
        {
            var manifestEntry = archive.GetEntry(ManifestEntry)
                ?? throw new BackupException("Not a Nordvik backup: manifest.json is missing.");

            BackupManifest? manifest;
            try
            {
                using var stream = manifestEntry.Open();
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream);
            }
            catch (JsonException ex)
            {
                throw new BackupException("Not a Nordvik backup: manifest.json is unreadable.", ex);
            }

            if (manifest == null)
                throw new BackupException("Not a Nordvik backup: manifest.json is empty.");
            if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
                throw new BackupException($"Unsupported backup format version {manifest.FormatVersion}.");
            if (!string.Equals(manifest.Provider, "sqlite", StringComparison.OrdinalIgnoreCase))
                throw new BackupException($"Backup was made from a '{manifest.Provider}' database; only SQLite backups can be restored.");
            if (manifest.LatestMigrationId != null && !knownMigrations.Contains(manifest.LatestMigrationId))
                throw new BackupException("Backup was made by a newer version of Nordvik Manager. Update the server before restoring it.");
            if (archive.GetEntry(DatabaseEntry) == null)
                throw new BackupException("Backup is incomplete: the database is missing.");

            return manifest;
        }

        /// <summary>Ids of the EF migrations compiled into this app (no database needed).</summary>
        public static IReadOnlyCollection<string> KnownMigrations() =>
            typeof(DndOneContext).Assembly.GetTypes()
                .Where(t => typeof(Migration).IsAssignableFrom(t) && t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(DndOneContext))
                .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
                .OfType<string>()
                .ToHashSet();
    }
}
