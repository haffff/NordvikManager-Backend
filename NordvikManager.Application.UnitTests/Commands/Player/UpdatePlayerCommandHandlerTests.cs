using NordvikManager.Application.Commands.Game.UpdateGame;
using NordvikManager.Application.Commands.Player.UpdatePlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.UnitTests.Commands.Player
{
    public class UpdatePlayerCommandHandlerTests : HandlerTestBase
    {
        private UpdatePlayerCommandHandler Handler() => new(Db, Mapper);

        private UpdatePlayerCommand Rename(Guid gameId, string name) => new()
        {
            Player = Player(),
            GameId = gameId,
            playerDTO = new PlayerDTO { Id = PlayerId, Name = name },
        };

        [Fact]
        public async Task Handle_ReturnsNoChange_WhenSavedWithTheSameValues()
        {
            var game = BuildGame();

            var result = await Handler().Handle(Rename(game.Id, "Tester"), CancellationToken.None);

            Assert.Equal(CommandResponse.NoChange, result);
        }

        [Fact]
        public async Task Handle_SavesAndReturnsOk_WhenAValueChanged()
        {
            var game = BuildGame();

            var result = await Handler().Handle(Rename(game.Id, "Renamed"), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.Equal("Renamed", SeedContext().Players.Find(PlayerId)!.Name);
        }
    }
}
