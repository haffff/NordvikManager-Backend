using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Application.Exceptions;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Wasm.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WasmHost
{
    // The browser host's file storage (big uploads, files cards save as files) on OPFS, through
    // IBrowserFiles; here with a fake that behaves as opfs-files.js does: a file's handle is opened
    // once and reused, sync calls need it open, and moving or deleting a file lets go of it.
    // In ActionScenarios: building a WasmHost sets a process-wide static (see WasmHostTests).
    [Collection("ActionScenarios")]
    public class BrowserFileStorageTests
    {
        private static readonly Guid Game = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Resource = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private readonly FakeBrowserFiles _files = new();

        private Task<BrowserFileStorage> Open() => BrowserFileStorage.OpenAsync(_files);

        private static byte[] Bytes(int length, int seed = 1)
        {
            var data = new byte[length];
            new Random(seed).NextBytes(data);
            return data;
        }

        [Fact]
        public async Task SaveAsync_KeepsTheFileUnderStorageGames_AndReadsItBack()
        {
            var storage = await Open();
            var data = Bytes(1000);

            var path = await storage.SaveAsync(Game, Resource, data, ".png");

            Assert.Equal($"storage/games/{Game}/{Resource}.png", path);
            Assert.True(storage.Exists(path));
            Assert.Equal(data, await storage.ReadAsync(path));
            Assert.Equal(data, _files.Content(path));
        }

        [Fact]
        public async Task OpenAsync_OnTheSameFiles_FindsTheFile_WithTheSameStamp()
        {
            var first = await Open();
            var data = Bytes(5000);
            var path = await first.SaveAsync(Game, Resource, data, null);
            var stamp = first.GetStamp(path);

            _files.Reload(); // a reload: no handle is open any more
            var reloaded = await Open();

            Assert.True(reloaded.Exists(path));
            // The resource cache's version comes from the stamp: it mustn't change on a reload.
            Assert.Equal(stamp, reloaded.GetStamp(path));
            Assert.Equal(5000, reloaded.GetStamp(path)!.Length);
            Assert.Equal(data, await reloaded.ReadAsync(path));
        }

        [Fact]
        public async Task SaveAsync_OverAFile_WritesANewOne_AndMovesItInPlace()
        {
            var storage = await Open();
            var path = await storage.SaveAsync(Game, Resource, Bytes(3000, 1), null);
            _files.Log.Clear();

            var data = Bytes(1000, 2);
            await storage.SaveAsync(Game, Resource, data, null);

            // Never written in place: a tab closed half way leaves the old file whole.
            Assert.DoesNotContain(_files.Log, op => op.StartsWith("write " + path + " ") || op == "truncate " + path);
            Assert.Contains("move " + path + ".tmp " + path, _files.Log);
            Assert.Equal(data, await storage.ReadAsync(path));
            Assert.Equal(1000, storage.GetStamp(path)!.Length);
            Assert.Equal(new[] { path }, _files.Paths());
        }

        [Fact]
        public async Task SaveAsync_FlushesTheFile_BeforeItIsInPlace()
        {
            var storage = await Open();

            var path = await storage.SaveAsync(Game, Resource, Bytes(10), null);

            var flushed = _files.Log.IndexOf("flush " + path + ".tmp");
            var moved = _files.Log.IndexOf("move " + path + ".tmp " + path);
            Assert.True(flushed >= 0 && flushed < moved, string.Join(", ", _files.Log));
        }

        [Fact]
        public async Task ReadRangeAsync_ReadsOnlyThatPart_AndTwoAtOnceWork()
        {
            var storage = await Open();
            var data = Bytes(10_000);
            var path = await storage.SaveAsync(Game, Resource, data, ".ogg");

            var both = await Task.WhenAll(storage.ReadRangeAsync(path, 100, 50), storage.ReadRangeAsync(path, 120, 9_999));

            Assert.Equal(data.Skip(100).Take(50), both[0]!);
            Assert.Equal(data.Skip(120), both[1]!);
            Assert.Empty((await storage.ReadRangeAsync(path, 10_000, 10))!);
        }

        [Fact]
        public async Task MoveInAsync_CopiesTheFinishedUpload_InParts_AndRemovesIt()
        {
            var storage = await Open();
            var upload = Path.GetTempFileName();
            var data = Bytes(2_500_000);
            await File.WriteAllBytesAsync(upload, data);

            var path = await storage.MoveInAsync(Game, Resource, upload, ".mp4");

            Assert.Equal($"storage/games/{Game}/{Resource}.mp4", path);
            Assert.False(File.Exists(upload));
            Assert.Equal(data, _files.Content(path));
            Assert.Equal(2_500_000, storage.GetStamp(path)!.Length);
            // Not read whole: a 1 GB upload would be in memory twice.
            Assert.True(_files.LargestWrite <= BrowserFileStorage.CopyPart, _files.LargestWrite.ToString());
        }

        [Fact]
        public async Task OpenAsync_DeletesASaveCutShort()
        {
            var first = await Open();
            var path = await first.SaveAsync(Game, Resource, Bytes(10), null);
            await _files.OpenAsync(path + ".tmp", create: true); // the tab closed before the move
            _files.WriteAt(path + ".tmp", Bytes(5), 0);
            _files.Reload();

            var reopened = await Open();

            Assert.Equal(new[] { path }, _files.Paths());
            Assert.False(reopened.Exists(path + ".tmp"));
            Assert.Equal(10, reopened.GetStamp(path)!.Length);
        }

        [Fact]
        public async Task DeleteAsync_RemovesTheFile()
        {
            var storage = await Open();
            var path = await storage.SaveAsync(Game, Resource, Bytes(10), null);

            await storage.DeleteAsync(path);

            Assert.False(storage.Exists(path));
            Assert.Null(storage.GetStamp(path));
            Assert.Null(await storage.ReadAsync(path));
            Assert.Empty(_files.Paths());
        }

        [Fact]
        public async Task AFileItDoesNotHave_IsMissing()
        {
            var storage = await Open();

            // e.g. a path kept by an older host, whose files were in the tab's memory
            Assert.False(storage.Exists("./data/storage/games/x/y"));
            Assert.Null(await storage.ReadAsync("./data/storage/games/x/y"));
            Assert.Null(await storage.ReadRangeAsync("./data/storage/games/x/y", 0, 10));
            Assert.Null(storage.GetStamp(""));
            Assert.Empty(storage.ListDirectory(null));
        }

        [Fact]
        public async Task ABrowserWithNoRoomLeft_IsAnUploadLimit_AndLeavesNothingBehind()
        {
            var storage = await Open();
            _files.Room = 100;

            var error = await Assert.ThrowsAsync<UploadLimitException>(() => storage.SaveAsync(Game, Resource, Bytes(1000), null));

            Assert.Contains("browser", error.Message);
            Assert.Empty(_files.Paths());
            Assert.False(storage.Exists($"storage/games/{Game}/{Resource}"));
        }

        [Fact]
        public async Task WasmHost_WithBrowserFiles_KeepsFilesThere()
        {
            var storage = await Open();
            await using var provider = NordvikManager.Wasm.Hosting.WasmHost.Build(
                NordvikManager.Wasm.Hosting.WasmHost.Configuration("http://central.test"), new WasmHostTests.NoBrowserRtc(), files: storage);

            Assert.Same(storage, provider.GetRequiredService<IFileStorageProvider>());
        }
    }

    /// <summary>opfs-files.js as the C# side sees it, in memory.</summary>
    internal class FakeBrowserFiles : IBrowserFiles
    {
        private readonly Dictionary<string, (List<byte> Data, double Modified)> _files = new();
        private readonly HashSet<string> _open = new();
        private double _clock = 1_700_000_000_000;

        public List<string> Log { get; } = new();
        public int LargestWrite { get; private set; }
        public long Room { get; set; } = long.MaxValue;

        public byte[] Content(string path) => _files[path].Data.ToArray();
        public string[] Paths() => _files.Keys.OrderBy(p => p).ToArray();
        public void Reload() => _open.Clear();

        private void NeedOpen(string path)
        {
            if (!_open.Contains(path))
                throw new InvalidOperationException($"{path} isn't open");
        }

        public async Task<IReadOnlyList<BrowserFile>> ListAsync(string folder)
        {
            await Task.Yield();
            return _files.Where(f => f.Key.StartsWith(folder + "/"))
                .Select(f => new BrowserFile(f.Key, f.Value.Data.Count, f.Value.Modified)).ToList();
        }

        public async Task OpenAsync(string path, bool create)
        {
            await Task.Yield();
            if (!_files.ContainsKey(path))
            {
                if (!create)
                    throw new FileNotFoundException(path);
                _files[path] = (new List<byte>(), _clock++);
            }
            _open.Add(path);
            Log.Add("open " + path);
        }

        public int ReadAt(string path, Span<byte> buffer, long at)
        {
            NeedOpen(path);
            var data = _files[path].Data;
            var n = (int)Math.Max(0, Math.Min(buffer.Length, data.Count - at));
            for (var i = 0; i < n; i++)
                buffer[i] = data[(int)at + i];
            return n;
        }

        public void WriteAt(string path, ReadOnlySpan<byte> data, long at)
        {
            NeedOpen(path);
            if (_files.Values.Sum(f => (long)f.Data.Count) + data.Length > Room)
                throw new BrowserStorageFullException();
            LargestWrite = Math.Max(LargestWrite, data.Length);
            Log.Add($"write {path} {at}");
            var file = _files[path].Data;
            while (file.Count < at + data.Length)
                file.Add(0);
            for (var i = 0; i < data.Length; i++)
                file[(int)at + i] = data[i];
            _files[path] = (file, _clock++);
        }

        public void Truncate(string path, long size)
        {
            NeedOpen(path);
            Log.Add("truncate " + path);
            var file = _files[path].Data;
            if (file.Count > size)
                file.RemoveRange((int)size, file.Count - (int)size);
            _files[path] = (file, _clock++);
        }

        public void Flush(string path)
        {
            NeedOpen(path);
            Log.Add("flush " + path);
        }

        public void Close(string path)
        {
            _open.Remove(path);
            Log.Add("close " + path);
        }

        public async Task MoveAsync(string from, string to)
        {
            await Task.Yield();
            _open.Remove(from);
            _open.Remove(to);
            _files[to] = _files[from];
            _files.Remove(from);
            Log.Add($"move {from} {to}");
        }

        public async Task DeleteAsync(string path)
        {
            await Task.Yield();
            _open.Remove(path);
            _files.Remove(path);
            Log.Add("delete " + path);
        }

        public async Task<double> ModifiedAsync(string path)
        {
            await Task.Yield();
            return _files[path].Modified;
        }
    }
}
