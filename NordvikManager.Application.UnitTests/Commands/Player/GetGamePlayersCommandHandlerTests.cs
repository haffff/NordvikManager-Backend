using NordvikManager.Application.Commands.Player.GetGamePlayers;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.UnitTests.Commands.Player
{
    // Chat whispers address players by name and the GM by role, so the chat needs every
    // player of the game (also those not connected) and who the GM is.
    public class GetGamePlayersCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid gmId = Guid.NewGuid();
        private readonly Guid bobId = Guid.NewGuid();

        public GetGamePlayersCommandHandlerTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "g", SystemPlayerId = Guid.NewGuid(), MasterId = gmId,
                Players = new List<PlayerModel>
                {
                    new PlayerModel { Id = gmId, Name = "Dungeon Master" },
                    new PlayerModel { Id = bobId, Name = "Bob" },
                    new PlayerModel { Id = Guid.NewGuid(), Name = "System", System = true },
                },
            });
            seed.Games.Add(new GameModel
            {
                Id = Guid.NewGuid(), Name = "other", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = Guid.NewGuid(), Name = "Stranger" } },
            });
            seed.SaveChanges();
        }

        private Task<List<DataTransferObjects.Game.PlayerDTO>> Get() =>
            new GetGamePlayersCommandHandler(Db, Mapper).Handle(new GetGamePlayersCommand { GameId = gameId }, CancellationToken.None);

        [Fact]
        public async Task Handle_ReturnsThePlayersOfTheGame_WithoutTheSystemPlayer()
        {
            var players = await Get();

            Assert.Equal(new[] { "Bob", "Dungeon Master" }, players.Select(p => p.Name).OrderBy(n => n));
        }

        [Fact]
        public async Task Handle_MarksTheGm()
        {
            var players = await Get();

            Assert.True(players.Single(p => p.Id == gmId).IsOwner);
            Assert.False(players.Single(p => p.Id == bobId).IsOwner);
        }
    }
}
