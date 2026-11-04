using NordvikManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;

namespace NordvikManager.Infrastructure.Services.Storage
{
    public class LocalFileStorageProvider : IFileStorageProvider
    {
        private readonly string basePath;

        public LocalFileStorageProvider(IConfiguration configuration)
        {
            basePath = configuration.GetSection("FileStorage")["BasePath"] ?? "./data/storage";
        }

        public async Task<string> SaveAsync(Guid gameId, Guid resourceId, byte[] data, string? fileExtension)
        {
            var dir = Path.Combine(basePath, "games", gameId.ToString());
            Directory.CreateDirectory(dir);

            var fileName = resourceId.ToString() + (fileExtension ?? string.Empty);
            var fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            await File.WriteAllBytesAsync(fullPath, data);
            return fullPath;
        }

        public async Task<byte[]?> ReadAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            return await File.ReadAllBytesAsync(path);
        }

        public async Task<byte[]?> ReadRangeAsync(string path, long offset, int count)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
            if (offset >= stream.Length || count <= 0)
                return Array.Empty<byte>();
            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[(int)Math.Min(count, stream.Length - offset)];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read));
                if (n == 0) break;
                read += n;
            }
            return read == buffer.Length ? buffer : buffer[..read];
        }

        public Task DeleteAsync(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);

            return Task.CompletedTask;
        }

        public bool Exists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public FileStamp? GetStamp(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.LastWriteTimeUtc, info.Length) : null;
        }

        public IReadOnlyList<LocalDirectoryEntry> ListDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // No path given — start from drive roots (Windows) so the GM has somewhere to
                // begin browsing from.
                return DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new LocalDirectoryEntry
                    {
                        Name = d.Name,
                        FullPath = d.RootDirectory.FullName,
                        IsDirectory = true,
                        Size = null,
                    })
                    .ToList();
            }

            if (!Directory.Exists(path))
                return Array.Empty<LocalDirectoryEntry>();

            var entries = new List<LocalDirectoryEntry>();

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                entries.Add(new LocalDirectoryEntry
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true,
                    Size = null,
                });
            }

            foreach (var file in Directory.EnumerateFiles(path))
            {
                long? size = null;
                try { size = new FileInfo(file).Length; } catch { /* transient FS access issue, not fatal to the listing */ }

                entries.Add(new LocalDirectoryEntry
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false,
                    Size = size,
                });
            }

            return entries;
        }
    }
}
