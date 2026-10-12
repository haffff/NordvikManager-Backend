using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DndOnePlaceManager.Infrastructure.Services.Backup
{
    /// <summary>
    /// Whole-server backups: a zip of a consistent SQLite snapshot plus the managed resource
    /// files, kept in BackupOptions.Folder. Restores are only staged here — the files are
    /// swapped in by PendingRestoreApplier at the next startup, before anything opens the DB.
    /// </summary>
    public class BackupService : IBackupService
    {
        private const string TimestampFormat = "yyyyMMdd-HHmmssfff";

        private static readonly Regex NamePattern = new(
            @"^nordvik-backup-(?<ts>\d{8}-\d{9})-(?<kind>manual|scheduled|pre-restore|imported)(?:-(?<label>[a-z0-9-]+))?\.zip$",
            RegexOptions.Compiled);

        private readonly BackupOptions options;
        private readonly TimeProvider time;
        private readonly IReadOnlyCollection<string> knownMigrations;
        // One backup / import / staging at a time — they all read or write the same files.
        private readonly SemaphoreSlim gate = new(1, 1);

        public BackupService(BackupOptions options, TimeProvider time, IReadOnlyCollection<string>? knownMigrations = null)
        {
            this.options = options;
            this.time = time;
            this.knownMigrations = knownMigrations ?? RestoreValidator.KnownMigrations();
        }

        public bool IsSupported => options.IsSupported;

        public async Task<BackupInfo> CreateAsync(BackupKind kind, string? label, CancellationToken cancellationToken = default)
        {
            EnsureSupported();
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() => CreateCore(kind, label, cancellationToken), cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public IReadOnlyList<BackupInfo> List()
        {
            if (!Directory.Exists(options.Folder))
                return Array.Empty<BackupInfo>();

            return Directory.EnumerateFiles(options.Folder, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(path => TryParse(Path.GetFileName(path), new FileInfo(path).Length))
                .OfType<BackupInfo>()
                .OrderByDescending(b => b.CreatedAtUtc)
                .ToList();
        }

        public Stream OpenRead(string name) =>
            new FileStream(ExistingPath(name), FileMode.Open, FileAccess.Read, FileShare.Read);

        public void Delete(string name) => File.Delete(ExistingPath(name));

        public async Task<BackupInfo> ImportAsync(Stream zip, CancellationToken cancellationToken = default)
        {
            EnsureSupported();
            await gate.WaitAsync(cancellationToken);
            var temp = Path.Combine(options.Folder, $".upload-{Guid.NewGuid():N}.zip");
            try
            {
                Directory.CreateDirectory(options.Folder);
                await using (var file = File.Create(temp))
                    await zip.CopyToAsync(file, cancellationToken);

                using (var archive = OpenArchive(temp))
                    RestoreValidator.Validate(archive, knownMigrations);

                var name = NewName(BackupKind.Imported, null);
                File.Move(temp, Path.Combine(options.Folder, name));
                return TryParse(name, new FileInfo(Path.Combine(options.Folder, name)).Length)!;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
                gate.Release();
            }
        }

        public async Task<PendingRestoreInfo> StageRestoreAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureSupported();
            var path = ExistingPath(name);
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() =>
                {
                    using var archive = OpenArchive(path);
                    RestoreValidator.Validate(archive, knownMigrations);

                    // Whatever is live now gets its own backup first, so the restore can be undone.
                    CreateCore(BackupKind.PreRestore, null, cancellationToken);

                    DeleteDirectory(options.StagingFolder);
                    try
                    {
                        ExtractForRestore(archive);
                    }
                    catch
                    {
                        DeleteDirectory(options.StagingFolder);
                        throw;
                    }

                    var pending = new PendingRestoreInfo(name, time.GetUtcNow().UtcDateTime);
                    BackupMarkers.Write(options.PendingMarkerPath, pending);
                    BackupMarkers.Delete(options.FailedMarkerPath);
                    return pending;
                }, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public PendingRestoreInfo? GetPendingRestore() => BackupMarkers.Read(options.PendingMarkerPath);

        public PendingRestoreInfo? GetFailedRestore() => BackupMarkers.Read(options.FailedMarkerPath);

        public void CancelPendingRestore()
        {
            gate.Wait();
            try
            {
                BackupMarkers.Delete(options.PendingMarkerPath);
                DeleteDirectory(options.StagingFolder);
            }
            finally
            {
                gate.Release();
            }
        }

        public int PruneScheduled(int keep)
        {
            var stale = List().Where(b => b.Kind == BackupKind.Scheduled).Skip(Math.Max(keep, 0)).ToList();
            foreach (var backup in stale)
                File.Delete(Path.Combine(options.Folder, backup.Name));
            return stale.Count;
        }

        // ---------------------------------------------------------------------

        private BackupInfo CreateCore(BackupKind kind, string? label, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(options.Folder);
            var name = NewName(kind, label);
            var snapshot = Path.Combine(options.Folder, $".snapshot-{Guid.NewGuid():N}.db");
            var tempZip = Path.Combine(options.Folder, $".tmp-{Guid.NewGuid():N}.zip");

            try
            {
                var manifest = SnapshotDatabase(snapshot);
                manifest.Kind = KindToken(kind);
                manifest.CreatedAtUtc = time.GetUtcNow().UtcDateTime;

                using (var file = File.Create(tempZip))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(snapshot, RestoreValidator.DatabaseEntry, CompressionLevel.Optimal);
                    manifest.Files = AddStorageFiles(archive, cancellationToken);

                    using var manifestStream = archive.CreateEntry(RestoreValidator.ManifestEntry).Open();
                    JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
                }

                // Only a finished zip gets a backup name, so List() never shows a half-written one.
                var target = Path.Combine(options.Folder, name);
                File.Move(tempZip, target);
                return TryParse(name, new FileInfo(target).Length)!;
            }
            finally
            {
                if (File.Exists(snapshot)) File.Delete(snapshot);
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        /// <summary>
        /// Copies the live DB with SQLite's online backup API — consistent even while the app is
        /// writing, unlike copying data.db (and its -wal) as files — then reads the manifest
        /// facts from the copy.
        /// </summary>
        private BackupManifest SnapshotDatabase(string snapshotPath)
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = options.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshotPath, Pooling = false }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);

            var manifest = new BackupManifest
            {
                FormatVersion = BackupManifest.CurrentFormatVersion,
                Provider = "sqlite",
                AppVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                LatestMigrationId = TryScalar(destination, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1") as string,
                Games = Convert.ToInt32(TryScalar(destination, "SELECT COUNT(*) FROM Games") ?? 0),
                Resources = Convert.ToInt32(TryScalar(destination, "SELECT COUNT(*) FROM Resources") ?? 0),
            };

            using var command = destination.CreateCommand();
            command.CommandText = $"SELECT Id, GameId, Path FROM Resources WHERE Storage = {(int)Domain.Enums.ResourceStorageKind.Linked}";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                manifest.LinkedResources.Add(new LinkedResourceInfo(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));

            return manifest;
        }

        private int AddStorageFiles(ZipArchive archive, CancellationToken cancellationToken)
        {
            var games = options.GamesStoragePath;
            if (!Directory.Exists(games))
                return 0;

            var count = 0;
            foreach (var file in Directory.EnumerateFiles(games, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entryName = RestoreValidator.StoragePrefix + "games/" + Path.GetRelativePath(games, file).Replace('\\', '/');
                try
                {
                    // Resource files are mostly already-compressed media.
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
                    count++;
                }
                catch (FileNotFoundException)
                {
                    // Deleted between listing and reading — it's gone from the game too.
                }
            }
            return count;
        }

        private void ExtractForRestore(ZipArchive archive)
        {
            var staging = Path.GetFullPath(options.StagingFolder);
            Directory.CreateDirectory(staging);

            foreach (var entry in archive.Entries)
            {
                string target;
                if (entry.FullName == RestoreValidator.DatabaseEntry)
                    target = Path.Combine(staging, "data.db");
                else if (entry.FullName.StartsWith(RestoreValidator.StoragePrefix + "games/", StringComparison.Ordinal) && entry.Name != "")
                    target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                else
                    continue;

                if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new BackupException($"Backup contains an unsafe path: {entry.FullName}");

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }

        private string NewName(BackupKind kind, string? label)
        {
            var now = time.GetUtcNow().UtcDateTime;
            var slug = Slug(label);
            while (true)
            {
                var name = $"nordvik-backup-{now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}-{KindToken(kind)}{(slug == null ? "" : "-" + slug)}.zip";
                if (!File.Exists(Path.Combine(options.Folder, name)))
                    return name;
                now = now.AddMilliseconds(1);
            }
        }

        private string ExistingPath(string name)
        {
            if (string.IsNullOrEmpty(name) || !NamePattern.IsMatch(name))
                throw new BackupException($"'{name}' is not a backup name.");
            var path = Path.Combine(options.Folder, name);
            if (!File.Exists(path))
                throw new BackupNotFoundException(name);
            return path;
        }

        private void EnsureSupported()
        {
            if (!options.IsSupported)
                throw new BackupNotSupportedException();
        }

        private static BackupInfo? TryParse(string name, long size)
        {
            var match = NamePattern.Match(name);
            if (!match.Success)
                return null;
            var created = DateTime.SpecifyKind(
                DateTime.ParseExact(match.Groups["ts"].Value, TimestampFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc);
            var label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
            return new BackupInfo(name, ParseKind(match.Groups["kind"].Value), created, size, label);
        }

        private static string KindToken(BackupKind kind) => kind switch
        {
            BackupKind.Scheduled => "scheduled",
            BackupKind.PreRestore => "pre-restore",
            BackupKind.Imported => "imported",
            _ => "manual",
        };

        private static BackupKind ParseKind(string token) => token switch
        {
            "scheduled" => BackupKind.Scheduled,
            "pre-restore" => BackupKind.PreRestore,
            "imported" => BackupKind.Imported,
            _ => BackupKind.Manual,
        };

        private static string? Slug(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return null;
            var slug = Regex.Replace(label.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
            return slug.Length == 0 ? null : slug;
        }

        private static ZipArchive OpenArchive(string path)
        {
            try
            {
                return ZipFile.OpenRead(path);
            }
            catch (InvalidDataException ex)
            {
                throw new BackupException("Not a Nordvik backup: the file is not a zip archive.", ex);
            }
        }

        private static object? TryScalar(SqliteConnection connection, string sql)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var value = command.ExecuteScalar();
                return value is DBNull ? null : value;
            }
            catch (SqliteException)
            {
                return null;
            }
        }

        internal static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    internal static class BackupMarkers
    {
        public static PendingRestoreInfo? Read(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonSerializer.Deserialize<PendingRestoreInfo>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static void Write(string path, PendingRestoreInfo info) =>
            File.WriteAllText(path, JsonSerializer.Serialize(info));

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
