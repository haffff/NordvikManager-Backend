using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Infrastructure.Services.Storage
{
    // Each upload is <storage>/uploads/<id>.part (the bytes so far) and <id>.json (what it is).
    // What has arrived is the .part file's length, so it survives a restart and a client can
    // resume from it. Parts of one upload are appended one at a time.
    public class LocalUploadStore : IUploadStore
    {
        private readonly string folder;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

        public LocalUploadStore(IConfiguration configuration)
        {
            var basePath = configuration.GetSection("FileStorage")["BasePath"] ?? "./data/storage";
            folder = Path.GetFullPath(Path.Combine(basePath, "uploads"));
        }

        public string PathOf(Guid id) => Path.Combine(folder, id.ToString("N") + ".part");
        private string MetaOf(Guid id) => Path.Combine(folder, id.ToString("N") + ".json");

        public UploadInfo Start(UploadInfo info)
        {
            Directory.CreateDirectory(folder);
            info.Id = Guid.NewGuid();
            info.CreatedUtc = DateTime.UtcNow;
            info.Received = 0;
            File.WriteAllBytes(PathOf(info.Id), Array.Empty<byte>());
            File.WriteAllText(MetaOf(info.Id), JsonSerializer.Serialize(info));
            return info;
        }

        public UploadInfo? Get(Guid id)
        {
            var meta = MetaOf(id);
            var part = PathOf(id);
            if (!File.Exists(meta) || !File.Exists(part))
                return null;
            var info = JsonSerializer.Deserialize<UploadInfo>(File.ReadAllText(meta));
            if (info == null)
                return null;
            info.Received = new FileInfo(part).Length;
            return info;
        }

        public async Task<long> AppendAsync(Guid id, long offset, byte[] data)
        {
            var gate = locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                var info = Get(id) ?? throw new FileNotFoundException("No such upload.");
                if (offset != info.Received || info.Received + data.Length > info.Size)
                    throw new UploadOffsetException(info.Received);
                await using var stream = new FileStream(PathOf(id), FileMode.Append, FileAccess.Write, FileShare.Read);
                await stream.WriteAsync(data);
                return info.Received + data.Length;
            }
            finally
            {
                gate.Release();
            }
        }

        public Task<byte[]> ReadAllAsync(Guid id) => File.ReadAllBytesAsync(PathOf(id));

        public void Delete(Guid id)
        {
            if (File.Exists(PathOf(id))) File.Delete(PathOf(id));
            if (File.Exists(MetaOf(id))) File.Delete(MetaOf(id));
            locks.TryRemove(id, out _);
        }

        public int DeleteOlderThan(TimeSpan age)
        {
            if (!Directory.Exists(folder))
                return 0;
            var removed = 0;
            foreach (var part in Directory.EnumerateFiles(folder, "*.part"))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(part) < age)
                    continue;
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(part), "N", out var id))
                {
                    Delete(id);
                    removed++;
                }
            }
            return removed;
        }
    }
}
