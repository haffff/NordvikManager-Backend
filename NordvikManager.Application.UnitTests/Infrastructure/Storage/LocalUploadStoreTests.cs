using Microsoft.Extensions.Configuration;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Storage;

namespace NordvikManager.Application.UnitTests.Infrastructure.Storage
{
    // Uploads arriving in parts: kept in a file under the storage folder until they're
    // finished, so what has arrived is simply the file's length (and survives a restart).
    public class LocalUploadStoreTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nm-uploads-" + Guid.NewGuid().ToString("N"));
        private readonly LocalUploadStore _store;

        public LocalUploadStoreTests()
        {
            _store = new LocalUploadStore(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _folder })
                .Build());
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        private UploadInfo NewUpload(long size = 6) => _store.Start(new UploadInfo
        {
            GameId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = "book.pdf", MimeType = "application/pdf",
            StorageKind = ResourceStorageKind.ManagedFile, Size = size,
        });

        [Fact]
        public async Task Parts_AddUp_AndReadBackWhole()
        {
            var upload = NewUpload();
            Assert.Equal(0, upload.Received);

            Assert.Equal(3, await _store.AppendAsync(upload.Id, 0, new byte[] { 1, 2, 3 }));
            Assert.Equal(6, await _store.AppendAsync(upload.Id, 3, new byte[] { 4, 5, 6 }));

            Assert.Equal(6, _store.Get(upload.Id)!.Received);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, await _store.ReadAllAsync(upload.Id));
            Assert.Equal("book.pdf", _store.Get(upload.Id)!.Name);
        }

        // An upload nothing arrived for in a while was given up (a reload starts it again): it
        // no longer counts against the upload limit, though its bytes stay until cleaned up.
        [Fact]
        public async Task Unfinished_ListsOnlyUploadsStillArriving()
        {
            var fresh = NewUpload();
            var stale = _store.Start(new UploadInfo { GameId = fresh.GameId, PlayerId = Guid.NewGuid(), Name = "a", Size = 6 });
            var finished = _store.Start(new UploadInfo { GameId = fresh.GameId, PlayerId = Guid.NewGuid(), Name = "b", Size = 1 });
            await _store.AppendAsync(finished.Id, 0, new byte[] { 1 });
            _store.MarkFinished(finished.Id, Guid.NewGuid());
            File.SetLastWriteTimeUtc(_store.PathOf(stale.Id), DateTime.UtcNow.AddMinutes(-30));

            var arriving = _store.Unfinished(fresh.GameId, TimeSpan.FromMinutes(10));

            Assert.Equal(new[] { fresh.Id }, arriving.Select(u => u.Id).ToArray());
            Assert.Empty(_store.Unfinished(Guid.NewGuid(), TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public async Task Finished_IsRemembered_WithoutItsBytes_AndTakesNoMoreParts()
        {
            var upload = NewUpload(size: 3);
            await _store.AppendAsync(upload.Id, 0, new byte[] { 1, 2, 3 });
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant(), await _store.HashAsync(upload.Id));
            var resourceId = Guid.NewGuid();

            _store.MarkFinished(upload.Id, resourceId);

            Assert.False(File.Exists(_store.PathOf(upload.Id)));
            Assert.Equal(resourceId, _store.Get(upload.Id)!.ResourceId);
            Assert.Equal(3, _store.Get(upload.Id)!.Received);
            await Assert.ThrowsAsync<UploadOffsetException>(() => _store.AppendAsync(upload.Id, 3, new byte[] { 9 }));
        }

        [Fact]
        public async Task FileStorage_MovesAFileIn_WithoutCopyingItsBytesThroughMemory()
        {
            var files = new LocalFileStorageProvider(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _folder }).Build());
            var upload = NewUpload(size: 2);
            await _store.AppendAsync(upload.Id, 0, new byte[] { 5, 6 });
            var gameId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();

            var path = await files.MoveInAsync(gameId, resourceId, _store.PathOf(upload.Id), null);

            Assert.False(File.Exists(_store.PathOf(upload.Id)));
            Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(path));
            Assert.EndsWith(resourceId.ToString(), path);
        }

        [Fact]
        public async Task APart_AtTheWrongPlace_OrPastTheSize_IsRefused()
        {
            var upload = NewUpload(size: 4);
            await _store.AppendAsync(upload.Id, 0, new byte[] { 1, 2 });

            await Assert.ThrowsAsync<UploadOffsetException>(() => _store.AppendAsync(upload.Id, 0, new byte[] { 9 }));
            await Assert.ThrowsAsync<UploadOffsetException>(() => _store.AppendAsync(upload.Id, 5, new byte[] { 9 }));
            await Assert.ThrowsAsync<UploadOffsetException>(() => _store.AppendAsync(upload.Id, 2, new byte[] { 3, 4, 5 }));
            Assert.Equal(2, _store.Get(upload.Id)!.Received);
        }

        [Fact]
        public async Task Survives_ANewStore_AndDeleteForgetsIt()
        {
            var upload = NewUpload();
            await _store.AppendAsync(upload.Id, 0, new byte[] { 1 });

            var again = new LocalUploadStore(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _folder }).Build());
            Assert.Equal(1, again.Get(upload.Id)!.Received);

            again.Delete(upload.Id);
            Assert.Null(_store.Get(upload.Id));
            Assert.Null(_store.Get(Guid.NewGuid()));
        }

        [Fact]
        public void DeleteOlderThan_DropsAbandonedUploads()
        {
            var old = NewUpload();
            var fresh = NewUpload();
            File.SetLastWriteTimeUtc(_store.PathOf(old.Id), DateTime.UtcNow.AddDays(-2));

            Assert.Equal(1, _store.DeleteOlderThan(TimeSpan.FromDays(1)));
            Assert.Null(_store.Get(old.Id));
            Assert.NotNull(_store.Get(fresh.Id));
        }
    }
}
