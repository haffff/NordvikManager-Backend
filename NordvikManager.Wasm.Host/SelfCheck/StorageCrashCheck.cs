using Microsoft.Data.Sqlite;
using System.Runtime.Versioning;

namespace NordvikManager.Wasm.Host.SelfCheck
{
    /// <summary>
    /// storage-e2e.cjs: SQLite on OPFS survives the tab being killed mid-commit. The writer
    /// (?selfcheck=crash-write) commits rows in a loop until the browser is closed; the checker
    /// (?selfcheck=crash-check, a new browser on the same profile) prints INTEGRITY with what it
    /// finds. Its own file (/opfs/crash.db), with the GM host's rollback journal.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static class StorageCrashCheck
    {
        public const string Database = "/opfs/crash.db";
        private const int RowBytes = 64 * 1024;

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
            for (var n = 1; ; n++)
            {
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

        public static void Check()
        {
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
                var journal = System.IO.File.Exists(Database + "-journal");
                Console.WriteLine($"INTEGRITY {(integrity == "ok" && bad == 0 && rows > 0 ? "ok" : "BAD")} integrity_check={integrity}, {rows} rows, {bad} damaged, journal left={journal}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INTEGRITY BAD {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static long Checksum(byte[] data)
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
