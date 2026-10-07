using DndOnePlaceManager.Application.Commands.Game.GameExists;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Game
{
    public class GameExistsCommandHandlerTests : HandlerTestBase
    {
        private GameExistsCommandHandler Handler() => new(Db, Mapper);

        [Fact]
        public async Task Handle_ExistingGame_True()
        {
            var game = BuildGame();

            Assert.True(await Handler().Handle(new GameExistsCommand { GameId = game.Id }, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_MissingGame_False()
        {
            Assert.False(await Handler().Handle(new GameExistsCommand { GameId = Guid.NewGuid() }, CancellationToken.None));
        }
    }
}
