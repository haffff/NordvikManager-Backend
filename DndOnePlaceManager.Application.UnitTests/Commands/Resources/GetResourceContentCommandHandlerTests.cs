using DndOnePlaceManager.Application.Commands.Resources.GetResourceContent;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using SkiaSharp;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // What players fetch over the data channel, with a version they can cache it under.
    // When their cached version is still current, only that answer goes back: no bytes
    // are read from the database or disk.
    public class GetResourceContentCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Mock<IFileStorageProvider> storage = new();
        private readonly Guid gameId = Guid.NewGuid();

        public GetResourceContentCommandHandlerTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "G", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "T" } },
            });
            seed.SaveChanges();
        }

        private GetResourceContentCommandHandler Handler() => new(Mapper, Db, storage.Object);

        private Task<ResourceContent?> Get(Guid id, string? ifVersion = null, bool thumbnail = false) =>
            Handler().Handle(new GetResourceContentCommand { GameID = gameId, ID = id, IfVersion = ifVersion, Thumbnail = thumbnail }, CancellationToken.None);

        private Guid Seed(Action<ResourceModel> setup)
        {
            var r = new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = PlayerId, Name = "r", MimeType = MimeType.MP3 };
            setup(r);
            using var seed = SeedContext();
            seed.Resources.Add(r);
            seed.SaveChanges();
            return r.Id;
        }

        private ResourceModel Stored(Guid id)
        {
            using var check = SeedContext();
            return check.Resources.Find(id)!;
        }

        private static byte[] Png(int width)
        {
            using var bitmap = new SKBitmap(width, 100);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }

        [Fact]
        public async Task Blob_ReturnsDataAndItsHash()
        {
            var data = new byte[] { 1, 2, 3 };
            var id = Seed(r => { r.Data = data; r.ContentHash = ResourceVersions.HashOf(data); });

            var result = await Get(id);

            Assert.Equal(data, result!.Data);
            Assert.Equal(ResourceVersions.HashOf(data), result.Version);
            Assert.False(result.NotModified);
            Assert.Equal(MimeType.MP3, result.MimeType);
        }

        [Fact]
        public async Task Blob_CurrentVersion_NotModified_WithoutReadingTheBytes()
        {
            var data = new byte[] { 1, 2, 3 };
            var id = Seed(r => { r.Data = data; r.ContentHash = ResourceVersions.HashOf(data); });
            Commands.Reset();

            var result = await Get(id, ifVersion: ResourceVersions.HashOf(data));

            Assert.True(result!.NotModified);
            Assert.Null(result.Data);
            Assert.DoesNotContain(Commands.Texts, sql => sql.Contains("\"Data\""));
        }

        [Fact]
        public async Task Blob_OldVersion_ReturnsCurrentData()
        {
            var data = new byte[] { 4, 5 };
            var id = Seed(r => { r.Data = data; r.ContentHash = ResourceVersions.HashOf(data); });

            var result = await Get(id, ifVersion: "something-older");

            Assert.False(result!.NotModified);
            Assert.Equal(data, result.Data);
        }

        // Rows written before ContentHash existed.
        [Fact]
        public async Task Blob_WithoutHash_HashIsComputedAndSaved()
        {
            var data = new byte[] { 6, 6 };
            var id = Seed(r => r.Data = data);

            var result = await Get(id);

            Assert.Equal(ResourceVersions.HashOf(data), result!.Version);
            Assert.Equal(ResourceVersions.HashOf(data), Stored(id).ContentHash);
        }

        [Fact]
        public async Task ManagedFile_CurrentVersion_DoesNotReadTheFile()
        {
            var id = Seed(r => { r.Storage = ResourceStorageKind.ManagedFile; r.Path = "C:/s/f"; r.ContentHash = "h1"; });

            var result = await Get(id, ifVersion: "h1");

            Assert.True(result!.NotModified);
            storage.Verify(s => s.ReadAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Linked_VersionFollowsTheFileStamp()
        {
            var id = Seed(r => { r.Storage = ResourceStorageKind.Linked; r.Path = "D:/music/a.mp3"; });
            var stamp = new FileStamp(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), 1000);
            storage.Setup(s => s.GetStamp("D:/music/a.mp3")).Returns(() => stamp);
            storage.Setup(s => s.ReadAsync("D:/music/a.mp3")).ReturnsAsync(new byte[] { 9 });

            var first = await Get(id);
            Assert.Equal(ResourceVersions.OfStamp(stamp), first!.Version);
            Assert.Equal(new byte[] { 9 }, first.Data);

            var again = await Get(id, ifVersion: first.Version);
            Assert.True(again!.NotModified);
            storage.Verify(s => s.ReadAsync(It.IsAny<string>()), Times.Once);

            stamp = new FileStamp(stamp.LastWriteUtc.AddMinutes(5), 1200); // edited on disk
            var changed = await Get(id, ifVersion: first.Version);
            Assert.False(changed!.NotModified);
            Assert.NotEqual(first.Version, changed.Version);
        }

        [Fact]
        public async Task Linked_FileMissing_NotFound()
        {
            var id = Seed(r => { r.Storage = ResourceStorageKind.Linked; r.Path = "D:/gone.mp3"; });
            storage.Setup(s => s.GetStamp("D:/gone.mp3")).Returns((FileStamp?)null);

            Assert.Null(await Get(id));
        }

        [Fact]
        public async Task UnknownResource_NotFound()
        {
            Assert.Null(await Get(Guid.NewGuid()));
        }

        [Fact]
        public async Task Thumbnail_HasItsOwnVersion_AndIsMadeOnce()
        {
            var png = Png(800);
            var id = Seed(r => { r.MimeType = MimeType.PNG; r.Data = png; r.ContentHash = ResourceVersions.HashOf(png); });

            var first = await Get(id, thumbnail: true);
            Assert.Equal("t-" + ResourceVersions.HashOf(png), first!.Version);
            Assert.True(first.Data!.Length < png.Length);
            Assert.Equal(ResourceVersions.HashOf(png), Stored(id).ThumbnailSourceVersion);

            Assert.True((await Get(id, ifVersion: first.Version, thumbnail: true))!.NotModified);
        }

        // A linked image edited on disk used to keep its first thumbnail forever.
        [Fact]
        public async Task Thumbnail_OfAChangedLinkedFile_IsRegenerated()
        {
            var id = Seed(r => { r.MimeType = MimeType.PNG; r.Storage = ResourceStorageKind.Linked; r.Path = "D:/map.png"; });
            var stamp = new FileStamp(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 10);
            var file = Png(800);
            storage.Setup(s => s.GetStamp("D:/map.png")).Returns(() => stamp);
            storage.Setup(s => s.ReadAsync("D:/map.png")).ReturnsAsync(() => file);
            var first = await Get(id, thumbnail: true);

            stamp = new FileStamp(stamp.LastWriteUtc.AddHours(1), 20);
            file = Png(400);
            var second = await Get(id, thumbnail: true);

            Assert.NotEqual(first!.Version, second!.Version);
            Assert.NotEqual(first.Data, second.Data);
            Assert.Equal(ResourceVersions.OfStamp(stamp), Stored(id).ThumbnailSourceVersion);
        }
    }
}
