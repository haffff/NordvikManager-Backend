using Microsoft.Data.Sqlite;
using System.Runtime.Versioning;
using NordvikManager.Wasm.Hosting;

namespace NordvikManager.Wasm.Host.SelfCheck
{
    /// <summary>
    /// storage-e2e.cjs: SQLite on OPFS survives the tab being killed mid-commit. The writer
    /// (?selfcheck=crash-write) commits rows in a loop until the browser is closed; the checker
    /// (?selfcheck=crash-check, a new browser on the same profile) prints INTEGRITY with what it
    /// finds. Its own file (/opfs/crash.db), with the GM host's rollback journal. Between commits it
    /// also saves one file again and again (BrowserFileStorage, its own crash-storage/ folder): after
    /// the cut the file must be one whole version (FILE ok), never a half-written one.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static class StorageCrashCheck
    {
        public const string Database = "/opfs/crash.db";
        private const int RowBytes = 64 * 1024;
        private const string Storage = "crash-storage";
        private const int FileBytes = 512 * 1024;
        private static readonly Guid FileGame = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid FileResource = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static string FilePath => $"{Storage}/games/{FileGame}/{FileResource}.bin";

        private static async Task<BrowserFileStorage> FilesAsync() =>
            await BrowserFileStorage.OpenAsync(await JsBrowserFiles.CreateAsync(), Storage);

        // A version of the file: random bytes after an 8-byte checksum of them.
        private static byte[] Version(Random random)
        {
            var data = new byte[FileBytes];
            random.NextBytes(data.AsSpan(8));
            BitConverter.TryWriteBytes(data, Checksum(data.AsSpan(8)));
            return data;
        }

        private static SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
            connection.Open();
            Execute(connection, "PRAGMA journal_mode=DELETE;");
            return connection;
        }

        public static async Task WriteForeverAsync()
        {
            System.IO.File.Delete(Database);
            using var connection = Open();
            Execute(connection, "CREATE TABLE rows (id INTEGER PRIMARY KEY, data BLOB NOT NULL, checksum INTEGER NOT NULL);");
            var random = new Random();
            var files = await FilesAsync();
            for (var n = 1; ; n++)
            {
                if (n % 5 == 1)
                    await files.SaveAsync(FileGame, FileResource, Version(random), ".bin");
                var data = new byte[RowBytes];
                random.NextBytes(data);
                // Two writes per commit, so one cut between them would show.
                using (var tx = connection.BeginTransaction())
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = tx;
                    insert.CommandText = "INSERT INTO rows (data, checksum) VALUES ($data, 0); UPDATE rows SET checksum = $sum WHERE id = last_insert_rowid();";
                    insert.Parameters.AddWithValue("$data", data);
                    insert.Parameters.AddWithValue("$sum", Checksum(data));
                    insert.ExecuteNonQuery();
                    tx.Commit();
                }
                if (n == 1 || n % 25 == 0)
                    Console.WriteLine($"WRITING {n} commits");
                await Task.Yield(); // the worker's event loop: closing the browser can land anywhere
            }
        }

        public static async Task CheckAsync()
        {
            try
            {
                var files = await FilesAsync();
                var data = await files.ReadAsync(FilePath);
                var whole = data != null && data.Length == FileBytes && BitConverter.ToInt64(data, 0) == Checksum(data.AsSpan(8));
                Console.WriteLine($"FILE {(whole ? "ok" : "BAD")} {(data == null ? "missing" : $"{data.Length} bytes")}, stamp {files.GetStamp(FilePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FILE BAD {ex.GetType().Name}: {ex.Message}");
            }
            try
            {
                using var connection = Open();
                var integrity = Scalar(connection, "PRAGMA integrity_check;");
                long rows = 0, bad = 0;
                using (var read = connection.CreateCommand())
                {
                    read.CommandText = "SELECT data, checksum FROM rows;";
                    using var reader = read.ExecuteReader();
                    while (reader.Read())
                    {
                        rows++;
                        var data = (byte[])reader[0];
                        if (data.Length != RowBytes || Checksum(data) != reader.GetInt64(1))
                            bad++;
                    }
                }
                Console.WriteLine($"INTEGRITY {(integrity == "ok" && bad == 0 && rows > 0 ? "ok" : "BAD")} integrity_check={integrity}, {rows} rows, {bad} damaged, journal left: {Journal()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INTEGRITY BAD {ex.GetType().Name}: {ex.Message}");
            }
        }

        // A journal still there after SQLite opened the file: a hot one (its header starts with
        // SQLite's journal magic, d9 d5 05 f9 20 a1 63 d7) should have been rolled back and deleted;
        // one without it (an unfinished header) isn't hot and is harmless.
        private static string Journal()
        {
            var path = Database + "-journal";
            if (!System.IO.File.Exists(path))
                return "none";
            var head = new byte[12];
            using var file = System.IO.File.OpenRead(path);
            var read = file.Read(head, 0, head.Length);
            var magic = read >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd9, 0xd5, 0x05, 0xf9, 0x20, 0xa1, 0x63, 0xd7 });
            return $"{file.Length} bytes, {(magic ? "HOT MAGIC" : "no magic")}, starts {Convert.ToHexString(head, 0, read)}";
        }

        private static long Checksum(ReadOnlySpan<byte> data)
        {
            long sum = 17;
            foreach (var b in data)
                sum = unchecked(sum * 31 + b);
            return sum;
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static string? Scalar(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar()?.ToString();
        }
    }
}
