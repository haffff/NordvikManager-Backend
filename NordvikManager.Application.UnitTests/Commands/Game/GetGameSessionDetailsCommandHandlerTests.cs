using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Game
{
    // Players see a game's cover image in the session list, read from the Central
    // Server before any WebRTC connection exists — so the GM backend sends it along
    // when it registers the session there, as a data URL.
    public class GetGameSessionDetailsCommandHandlerTests : HandlerTestBase
    {
        private GetGameSessionDetailsCommandHandler Handler() => new(Db, Mapper, Storage);

        private void AddCoverImage(Guid gameId, ResourceModel resource)
        {
            resource.GameId = gameId;
            resource.Key = "game_image";
            resource.Name = "Game Image";
            Db.Resources.Add(resource);
            Db.SaveChanges();
        }

        [Fact]
        public async Task Handle_GameWithCoverImage_ReturnsItAsDataUrl()
        {
            var game = BuildGame();
            AddCoverImage(game.Id, new ResourceModel { Data = new byte[] { 104, 105 }, MimeType = MimeType.PNG });

            var result = await Handler().Handle(new GetGameSessionDetailsCommand { GameId = game.Id }, CancellationToken.None);

            Assert.Equal("data:image/png;base64,aGk=", result!.Image);
        }

        [Fact]
        public async Task Handle_CoverImageStoredAsFile_ReadsItFromStorage()
        {
            var game = BuildGame();
            AddCoverImage(game.Id, new ResourceModel { Storage = ResourceStorageKind.ManagedFile, Path = "img.png", MimeType = MimeType.PNG });
            StorageMock.Setup(s => s.ReadAsync("img.png")).ReturnsAsync(new byte[] { 104, 105 });

            var result = await Handler().Handle(new GetGameSessionDetailsCommand { GameId = game.Id }, CancellationToken.None);

            Assert.Equal("data:image/png;base64,aGk=", result!.Image);
        }

        [Fact]
        public async Task Handle_GameWithoutCoverImage_ReturnsNoImage()
        {
            var game = BuildGame();

            var result = await Handler().Handle(new GetGameSessionDetailsCommand { GameId = game.Id }, CancellationToken.None);

            Assert.Null(result!.Image);
        }
    }
}
