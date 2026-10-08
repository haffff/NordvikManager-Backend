using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Domain.Entities.Resources;
using SkiaSharp;
using System.Text;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class GetResourceThumbnailCommandHandlerTests : HandlerTestBase
    {
        private GetResourceThumbnailCommandHandler Handler() => new(Mapper, Db, Storage);

        // A real, decodable PNG — needed since the handler actually runs it through
        // SkiaSharp's decoder, not just a byte-array stand-in.
        private static byte[] MakeTestPng(int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private Guid SeedResource(Guid gameId, byte[] data, MimeType mimeType, byte[]? thumbnailData = null)
        {
            var id = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel
            {
                Id = id,
                GameId = gameId,
                Name = "test-resource",
                Data = data,
                MimeType = mimeType,
                ThumbnailData = thumbnailData,
                PlayerId = PlayerId,
            });
            Db.SaveChanges();
            return id;
        }

        [Fact]
        public async Task Handle_ResourceNotFound_ReturnsNull()
        {
            var game = BuildGame();

            var (data, mimeType) = await Handler().Handle(new GetResourceThumbnailCommand
            {
                GameID = game.Id,
                ID = Guid.NewGuid(),
                Player = Player(),
            }, CancellationToken.None);

            Assert.Null(data);
            Assert.Equal(MimeType.None, mimeType);
        }

        [Fact]
        public async Task Handle_NonImageMimeType_ReturnsOriginalDataUnchanged()
        {
            var game = BuildGame();
            var jsonBytes = Encoding.UTF8.GetBytes("{\"not\":\"an image\"}");
            var id = SeedResource(game.Id, jsonBytes, MimeType.JSON);

            var (data, mimeType) = await Handler().Handle(new GetResourceThumbnailCommand
            {
                GameID = game.Id,
                ID = id,
                Player = Player(),
            }, CancellationToken.None);

            Assert.Equal(jsonBytes, data);
            Assert.Equal(MimeType.JSON, mimeType);
            Assert.Null(Db.Resources.Find(id)!.ThumbnailData);
        }

        [Fact]
        public async Task Handle_ImageWithNoCachedThumbnail_GeneratesAndCachesIt()
        {
            var game = BuildGame();
            var original = MakeTestPng(800, 600);
            var id = SeedResource(game.Id, original, MimeType.PNG);

            var (data, mimeType) = await Handler().Handle(new GetResourceThumbnailCommand
            {
                GameID = game.Id,
                ID = id,
                Player = Player(),
                MaxDimension = 100,
            }, CancellationToken.None);

            Assert.Equal(MimeType.PNG, mimeType);
            Assert.NotNull(data);
            Assert.True(data!.Length < original.Length, "Thumbnail should be smaller than the original.");

            using var decoded = SKBitmap.Decode(data);
            Assert.True(decoded.Width <= 100 && decoded.Height <= 100);

            // Cached on the row for next time.
            Assert.Equal(data, Db.Resources.Find(id)!.ThumbnailData);
        }

        [Fact]
        public async Task Handle_ImageWithCachedThumbnail_ReturnsCacheWithoutRegenerating()
        {
            var game = BuildGame();
            var original = MakeTestPng(800, 600);
            var cached = new byte[] { 1, 2, 3 }; // deliberately not a real image — proves it's never touched
            var id = SeedResource(game.Id, original, MimeType.PNG, thumbnailData: cached);

            var (data, mimeType) = await Handler().Handle(new GetResourceThumbnailCommand
            {
                GameID = game.Id,
                ID = id,
                Player = Player(),
            }, CancellationToken.None);

            Assert.Equal(MimeType.PNG, mimeType);
            Assert.Equal(cached, data);
        }

        [Fact]
        public async Task Handle_CorruptImageData_FallsBackToOriginalInsteadOfThrowing()
        {
            var game = BuildGame();
            var garbage = Encoding.UTF8.GetBytes("this is not a real png");
            var id = SeedResource(game.Id, garbage, MimeType.PNG);

            var (data, mimeType) = await Handler().Handle(new GetResourceThumbnailCommand
            {
                GameID = game.Id,
                ID = id,
                Player = Player(),
            }, CancellationToken.None);

            Assert.Equal(garbage, data);
            Assert.Equal(MimeType.PNG, mimeType);
            Assert.Null(Db.Resources.Find(id)!.ThumbnailData);
        }
    }
}
