using Microsoft.Extensions.Logging;
using System.Buffers.Binary;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>Where the browser host keeps its database file between page loads (IndexedDB in a browser).</summary>
    public interface IBrowserFileStore
    {
        /// <summary>The saved file, or null when nothing was saved yet.</summary>
        Task<byte[]?> LoadAsync();

        Task SaveAsync(byte[] contents);
    }

    /// <summary>
    /// Keeps the browser host's SQLite file across page loads: <see cref="RestoreAsync"/> puts the
    /// saved copy back before the host opens it; <see cref="SaveIfChangedAsync"/> (run every few
    /// seconds by <see cref="RunAsync"/>, and when the page is hidden) copies it to the store when
    /// SQLite's file change counter moved and no write transaction is underway. The database
    /// uses a rollback journal (WasmHost sets it), so a commit is in the file itself.
    /// </summary>
    public sealed class DatabaseFilePersistence
    {
        private const int HeaderSize = 100;
        private const int ChangeCounterOffset = 24;

        private readonly string _path;
        private readonly IBrowserFileStore _store;
        private readonly ILogger? _logger;
        private uint? _savedCounter;
        private long _savedLength;
        private bool _saving;

        public DatabaseFilePersistence(string path, IBrowserFileStore store, ILogger? logger = null)
        {
            _path = path;
            _store = store;
            _logger = logger;
        }

        /// <summary>Writes the saved copy to <paramref name="path"/>; false when there is none.</summary>
        public static async Task<bool> RestoreAsync(string path, IBrowserFileStore store)
        {
            var saved = await store.LoadAsync();
            if (saved == null || saved.Length == 0)
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllBytesAsync(path, saved);
            return true;
        }

        /// <summary>Saves the file when it changed since the last save; true when it did.</summary>
        public async Task<bool> SaveIfChangedAsync()
        {
            if (_saving || !File.Exists(_path) || Underway(_path + "-journal") || Underway(_path + "-wal"))
                return false;

            var header = new byte[HeaderSize];
            long length;
            using (var file = Open())
            {
                length = file.Length;
                if (length < HeaderSize || file.Read(header, 0, HeaderSize) < HeaderSize)
                    return false;
            }
            var counter = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(ChangeCounterOffset, 4));
            if (counter == _savedCounter && length == _savedLength)
                return false;

            _saving = true;
            try
            {
                byte[] contents;
                using (var file = Open())
                {
                    contents = new byte[file.Length];
                    file.ReadExactly(contents);
                }
                await _store.SaveAsync(contents);
                _savedCounter = BinaryPrimitives.ReadUInt32BigEndian(contents.AsSpan(ChangeCounterOffset, 4));
                _savedLength = contents.Length;
                _logger?.LogDebug("Saved the database ({Bytes} bytes)", contents.Length);
                return true;
            }
            finally
            {
                _saving = false;
            }
        }

        /// <summary>Saves changes every <paramref name="every"/> until cancelled.</summary>
        public async Task RunAsync(TimeSpan every, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(every, cancellationToken);
                    await SaveIfChangedAsync();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Saving the database to the browser failed; trying again");
                }
            }
        }

        // SQLite keeps the file open for writing: share it rather than lock it.
        private FileStream Open() => new(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        private static bool Underway(string journal) => File.Exists(journal) && new FileInfo(journal).Length > 0;
    }
}
