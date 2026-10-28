using Microsoft.Data.Sqlite;

namespace NordvikManager.Infrastructure.Services.Backup
{
    public enum PendingRestoreResult
    {
        None,
        Applied,
        Failed,
    }

    /// <summary>
    /// Applies a restore staged by BackupService.StageRestoreAsync. Runs at startup, after the
    /// host is built but before it starts — nothing has opened the database yet, so data.db
    /// and the managed files can be swapped as plain files. Database.Migrate() in
    /// Startup.Configure then brings an older backup's schema up to date.
    /// </summary>
    public static class PendingRestoreApplier
    {
        public static PendingRestoreResult Apply(BackupOptions options, Action<string>? log = null)
        {
            log ??= _ => { };
            var pending = BackupMarkers.Read(options.PendingMarkerPath);
            if (pending == null)
            {
                // A marker we can't read is as good as none — drop it rather than retry forever.
                BackupMarkers.Delete(options.PendingMarkerPath);
                return PendingRestoreResult.None;
            }

            var stagedDb = Path.Combine(options.StagingFolder, "data.db");
            var stagedGames = Path.Combine(options.StagingFolder, "storage", "games");
            var aside = Path.Combine(options.Folder, $".replaced-{DateTime.UtcNow:yyyyMMddHHmmss}");
            var moves = new Stack<(string from, string to, bool directory)>();

            try
            {
                if (!File.Exists(stagedDb))
                    throw new BackupException("The staged database is missing.");
                EnsureReadable(stagedDb);

                Directory.CreateDirectory(aside);

                // Live data aside first, so a failure can put it back.
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    MoveFile(options.DatabasePath + suffix, Path.Combine(aside, "data.db" + suffix), moves);
                MoveDirectory(options.GamesStoragePath, Path.Combine(aside, "games"), moves);

                Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);
                MoveFile(stagedDb, options.DatabasePath, moves);
                Directory.CreateDirectory(options.StorageBasePath);
                MoveDirectory(stagedGames, options.GamesStoragePath, moves);

                RewriteManagedFilePaths(options);
            }
            catch (Exception ex)
            {
                Rollback(moves, log);
                BackupMarkers.Write(options.FailedMarkerPath, pending);
                BackupMarkers.Delete(options.PendingMarkerPath);
                BackupService.DeleteDirectory(options.StagingFolder);
                TryDeleteDirectory(aside);
                log($"Restore of backup '{pending.BackupName}' failed and was rolled back: {ex.Message}");
                return PendingRestoreResult.Failed;
            }

            BackupMarkers.Delete(options.PendingMarkerPath);
            BackupService.DeleteDirectory(options.StagingFolder);
            // The replaced data is also in the pre-restore backup taken when this was staged.
            TryDeleteDirectory(aside);
            log($"Restored backup '{pending.BackupName}'.");
            return PendingRestoreResult.Applied;
        }

        /// <summary>
        /// ManagedFile rows store an absolute path, which pointed into the storage folder of
        /// the machine/folder the backup was made on. Point them at the same game/file under
        /// this server's storage folder.
        /// </summary>
        private static void RewriteManagedFilePaths(BackupOptions options)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = options.DatabasePath, Pooling = false }.ToString());
            connection.Open();

            var updates = new List<(string id, string path)>();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = $"SELECT Id, Path FROM Resources WHERE Storage = {(int)Domain.Enums.ResourceStorageKind.ManagedFile} AND Path IS NOT NULL";
                using var reader = select.ExecuteReader();
                while (reader.Read())
                {
                    // {base}/games/{gameDir}/{file} — keep the last two segments, whichever OS wrote them.
                    var segments = reader.GetString(1).Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length < 2)
                        continue;
                    var path = Path.GetFullPath(Path.Combine(options.GamesStoragePath, segments[^2], segments[^1]));
                    updates.Add((reader.GetString(0), path));
                }
            }

            using var transaction = connection.BeginTransaction();
            foreach (var (id, path) in updates)
            {
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE Resources SET Path = $path WHERE Id = $id";
                update.Parameters.AddWithValue("$path", path);
                update.Parameters.AddWithValue("$id", id);
                update.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        private static void EnsureReadable(string databasePath)
        {
            try
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA quick_check";
                if (command.ExecuteScalar() as string != "ok")
                    throw new BackupException("The staged database is damaged.");
            }
            catch (SqliteException ex)
            {
                throw new BackupException("The staged database is not a valid SQLite database.", ex);
            }
        }

        private static void MoveFile(string from, string to, Stack<(string, string, bool)> moves)
        {
            if (!File.Exists(from))
                return;
            File.Move(from, to);
            moves.Push((from, to, false));
        }

        private static void MoveDirectory(string from, string to, Stack<(string, string, bool)> moves)
        {
            if (!Directory.Exists(from))
                return;
            try
            {
                Directory.Move(from, to);
            }
            catch (IOException) when (Path.GetPathRoot(Path.GetFullPath(from)) != Path.GetPathRoot(Path.GetFullPath(to)))
            {
                // Directory.Move can't cross volumes (backup folder and storage on different drives).
                CopyDirectory(from, to);
                Directory.Delete(from, recursive: true);
            }
            moves.Push((from, to, true));
        }

        private static void CopyDirectory(string from, string to)
        {
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            Directory.CreateDirectory(to);
        }

        private static void Rollback(Stack<(string from, string to, bool directory)> moves, Action<string> log)
        {
            while (moves.Count > 0)
            {
                var (from, to, directory) = moves.Pop();
                try
                {
                    if (directory)
                    {
                        if (Directory.Exists(from)) Directory.Delete(from, recursive: true);
                        MoveDirectory(to, from, new Stack<(string, string, bool)>());
                    }
                    else
                    {
                        File.Move(to, from, overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    log($"Could not move '{to}' back to '{from}': {ex.Message}");
                }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { BackupService.DeleteDirectory(path); }
            catch (IOException) { /* leftovers in the backup folder are harmless */ }
        }
    }
}
