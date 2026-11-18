using System.Collections.Concurrent;
using NordvikManager.Application.Exceptions;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser host's file storage: uploads over <c>Uploads.MaxBlobSize</c> and files cards
    /// save as files, kept on OPFS (<see cref="IBrowserFiles"/>) under <c>storage/games/&lt;game&gt;/</c>
    /// instead of the runtime's memory, so they're there after a reload. The stored path is that
    /// relative one. What's there is listed once (<see cref="OpenAsync"/>) and kept up to date, so
    /// <see cref="Exists"/> and <see cref="GetStamp"/> stay synchronous.
    /// </summary>
    public class BrowserFileStorage : IFileStorageProvider
    {
        public const string Root = "storage";

        /// <summary>A finished upload is copied in parts of this size, not read whole.</summary>
        public const int CopyPart = 1024 * 1024;

        private readonly IBrowserFiles files;
        private readonly string root;
        private readonly ConcurrentDictionary<string, FileStamp> stamps = new();

        private BrowserFileStorage(IBrowserFiles files, string root)
        {
            this.files = files;
            this.root = root;
        }

        /// <param name="root">The OPFS folder: the GM's <see cref="Root"/>, or the self-check's own.</param>
        public static async Task<BrowserFileStorage> OpenAsync(IBrowserFiles files, string root = Root)
        {
            var storage = new BrowserFileStorage(files, root);
            foreach (var file in await files.ListAsync(root))
            {
                // a save cut short (the tab closed before it was moved in place)
                if (file.Path.EndsWith(".tmp", StringComparison.Ordinal))
                    await files.DeleteAsync(file.Path);
                else
                    storage.stamps[file.Path] = Stamp(file.Modified, file.Size);
            }
            return storage;
        }

        private static FileStamp Stamp(double modified, long size) =>
            new(DateTime.SpecifyKind(DateTime.UnixEpoch.AddMilliseconds(modified), DateTimeKind.Utc), size);

        private string PathOf(Guid gameId, Guid resourceId, string? extension) =>
            $"{root}/games/{gameId}/{resourceId}{extension}";

        public Task<string> SaveAsync(Guid gameId, Guid resourceId, byte[] data, string? fileExtension) =>
            WriteAsync(PathOf(gameId, resourceId, fileExtension), (temp) =>
            {
                files.WriteAt(temp, data, 0);
                return Task.FromResult((long)data.Length);
            });

        public Task<string> MoveInAsync(Guid gameId, Guid resourceId, string sourcePath, string? fileExtension) =>
            WriteAsync(PathOf(gameId, resourceId, fileExtension), async (temp) =>
            {
                // The finished upload is in the runtime's memory: copied, then deleted.
                var buffer = new byte[CopyPart];
                long at = 0;
                using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int read;
                    while ((read = await source.ReadAsync(buffer)) > 0)
                    {
                        files.WriteAt(temp, buffer.AsSpan(0, read), at);
                        at += read;
                    }
                }
                File.Delete(sourcePath);
                return at;
            });

        // Written to <path>.tmp, flushed, then moved in place: the database never points at a file
        // that isn't whole, and replacing a file never leaves a half-written one.
        private async Task<string> WriteAsync(string path, Func<string, Task<long>> write)
        {
            var temp = path + ".tmp";
            long size;
            try
            {
                await files.OpenAsync(temp, create: true);
                files.Truncate(temp, 0);
                size = await write(temp);
                files.Flush(temp);
                files.Close(temp);
            }
            catch (BrowserStorageFullException)
            {
                await files.DeleteAsync(temp);
                throw new UploadLimitException("The browser has no room left for this file. Free some space on this computer, or delete files this game doesn't need.");
            }
            catch
            {
                try { await files.DeleteAsync(temp); } catch { /* the first error is the one to tell */ }
                throw;
            }
            stamps.TryRemove(path, out _);
            await files.MoveAsync(temp, path);
            stamps[path] = Stamp(await files.ModifiedAsync(path), size);
            return path;
        }

        public async Task<byte[]?> ReadAsync(string path)
        {
            var stamp = GetStamp(path);
            return stamp == null ? null : await ReadRangeAsync(path, 0, (int)Math.Min(stamp.Length, int.MaxValue));
        }

        public async Task<byte[]?> ReadRangeAsync(string path, long offset, int count)
        {
            var stamp = GetStamp(path);
            if (stamp == null)
                return null;
            if (offset >= stamp.Length || count <= 0)
                return Array.Empty<byte>();
            await files.OpenAsync(path, create: false);
            var buffer = new byte[(int)Math.Min(count, stamp.Length - offset)];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = files.ReadAt(path, buffer.AsSpan(read), offset + read);
                if (n <= 0) break;
                read += n;
            }
            return read == buffer.Length ? buffer : buffer[..read];
        }

        public async Task DeleteAsync(string path)
        {
            if (stamps.TryRemove(path ?? "", out _))
                await files.DeleteAsync(path!);
        }

        public bool Exists(string path) => !string.IsNullOrEmpty(path) && stamps.ContainsKey(path);

        public FileStamp? GetStamp(string path) =>
            !string.IsNullOrEmpty(path) && stamps.TryGetValue(path, out var stamp) ? stamp : null;

        /// <summary>No disk to browse: linking files on a disk is a GM server's (server-only route).</summary>
        public IReadOnlyList<LocalDirectoryEntry> ListDirectory(string? path) => Array.Empty<LocalDirectoryEntry>();
    }
}
