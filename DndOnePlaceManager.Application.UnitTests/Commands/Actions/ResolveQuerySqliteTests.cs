using DndOnePlaceManager.Application.Commands.Actions.ResolveQuery;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using ActionModel = DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    // The allowlist (ids an action may query) must translate on SQLite and still cover
    // the game itself, its maps, cards, players and actions — and nothing from other games.
    public class ResolveQuerySqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Dictionary<string, Guid> ids = new()
        {
            ["map"] = Guid.NewGuid(), ["card"] = Guid.NewGuid(), ["action"] = Guid.NewGuid(), ["foreign"] = Guid.NewGuid(),
        };

        public ResolveQuerySqliteTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "Campaign", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "Hero" } },
                Maps = new List<MapModel> { new MapModel { Id = ids["map"], Name = "Dungeon" } },
                Cards = new List<CardModel> { new CardModel { Id = ids["card"], Name = "Goblin" } },
                Actions = new List<ActionModel> { new ActionModel { Id = ids["action"], Name = "Attack", Content = "", Prefix = "" } },
            });
            seed.Games.Add(new GameModel
            {
                Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>(),
                Cards = new List<CardModel> { new CardModel { Id = ids["foreign"], Name = "Spy" } },
            });
            seed.SaveChanges();
        }

        private Task<string> Resolve(Guid target) => new ResolveQueryCommandHandler(Db, Mapper).Handle(new ResolveQueryCommand
        {
            GameId = gameId, Player = new() { Id = PlayerId, Name = "Hero" }, Expression = "%q:{t}.id%",
            Variables = new() { ["t"] = target.ToString() },
        }, CancellationToken.None);

        [Fact]
        public async Task AllowsTheGamesOwnEntities()
        {
            foreach (var key in new[] { "map", "card", "action" })
                Assert.Equal(ids[key].ToString(), await Resolve(ids[key]));
            Assert.Equal(PlayerId.ToString(), await Resolve(PlayerId));
            Assert.Equal(gameId.ToString(), await Resolve(gameId));
        }

        [Fact]
        public async Task RejectsOtherGamesEntities()
        {
            Assert.Equal("", await Resolve(ids["foreign"]));
        }
    }
}
