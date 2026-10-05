using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using ActionModel = DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    // The per-type queries (generic name/id filters, Map.Game navigation) must translate on SQLite.
    public class ActionGetDataSqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid elementId = Guid.NewGuid();
        private readonly Guid cardId = Guid.NewGuid();

        public ActionGetDataSqliteTests()
        {
            using var seed = SeedContext();
            var game = new GameModel
            {
                Id = gameId, Name = "g", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "p" } },
                Maps = new List<MapModel> { new MapModel { Id = mapId, Name = "Dungeon", Elements = new List<ElementModel> { new ElementModel { Id = elementId } } } },
                Cards = new List<CardModel> { new CardModel { Id = cardId, Name = "Orc" }, new CardModel { Id = Guid.NewGuid(), Name = "Goblin" } },
                Layouts = new List<LayoutModel> { new LayoutModel { Id = Guid.NewGuid(), Name = "Main", Value = "{}" } },
                Actions = new List<ActionModel> { new ActionModel { Id = Guid.NewGuid(), Name = "Attack", Content = "", Prefix = "" } },
            };
            seed.Games.Add(game);
            seed.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), EntityName = "CardModel", Name = "Hostile", Value = "true", ParentID = cardId });
            seed.SaveChanges();
        }

        private Task<List<IGameDataTransferObject>> Get(string type, string? name = null, string? property = null) =>
            new ActionGetDataCommandHandler(Db, Mapper).Handle(new ActionGetDataCommand { GameID = gameId, EntityType = type, Name = name, Property = property }, CancellationToken.None);

        [Fact]
        public async Task ByName_EachType()
        {
            var map = (MapDTO)Assert.Single(await Get("MapModel", "Dungeon"));
            Assert.Equal(elementId, Assert.Single(map.Elements!).Id);
            Assert.Equal(cardId, ((CardDto)Assert.Single(await Get("CardModel", "Orc"))).Id);
            Assert.Single(await Get("LayoutModel", "Main"));
            Assert.Single(await Get("ActionModel", "Attack"));
        }

        [Fact]
        public async Task ByProperty_AndElements()
        {
            Assert.Equal(cardId, ((CardDto)Assert.Single(await Get("CardModel", property: "Hostile"))).Id);
            Assert.Equal(elementId, ((ElementDTO)Assert.Single(await Get("ElementModel"))).Id);
        }
    }
}
