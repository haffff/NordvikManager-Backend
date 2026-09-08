using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DndOnePlaceManager.Infrastructure.Services.Storage
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
