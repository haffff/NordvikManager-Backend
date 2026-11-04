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
