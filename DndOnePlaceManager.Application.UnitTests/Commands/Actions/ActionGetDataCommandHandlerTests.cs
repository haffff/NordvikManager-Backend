using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    public class ActionGetDataCommandHandlerTests : HandlerTestBase
    {
        private ActionGetDataCommandHandler Handler() => new(Db, Mapper);

        private MapModel SeedMap(GameModel game, string name = "Test Map")
        {
            var map = new MapModel
            {
                Id = Guid.NewGuid(), Name = name, GridSize = 50, GridVisible = true,
                Width = 1200, Height = 700, Game = game,
                Elements = new List<ElementModel>(),
                Properties = new List<PropertyModel>(),
            };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        [Fact]
        public async Task Handle_ById_ReturnsSingleMappedDto()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Dungeon");
            var cmd = new ActionGetDataCommand { GameID = game.Id, EntityType = "MapModel", ID = map.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            var dto = Assert.IsType<MapDTO>(result[0]);
            Assert.Equal("Dungeon", dto.Name);
        }

        [Fact]
        public async Task Handle_ByName_ReturnsMatchingMaps()
        {
            var game = BuildGame();
            SeedMap(game, "Dungeon");
            SeedMap(game, "Tavern");
            var cmd = new ActionGetDataCommand { GameID = game.Id, EntityType = "MapModel", Name = "Tavern" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Tavern", ((MapDTO)result[0]).Name);
        }

        [Fact]
        public async Task Handle_ByProperty_ReturnsEntitiesWithMatchingPropertyName()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Dungeon");
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), EntityName = "MapModel", Name = "Difficulty", Value = "Hard", ParentID = map.Id });
            Db.SaveChanges();
            var cmd = new ActionGetDataCommand { GameID = game.Id, EntityType = "MapModel", Property = "Difficulty" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Dungeon", ((MapDTO)result[0]).Name);
        }

        [Fact]
        public async Task Handle_NoFilters_MapModel_ReturnsAllMapsInGame()
        {
            var game = BuildGame();
            SeedMap(game, "Dungeon");
            SeedMap(game, "Tavern");
            var cmd = new ActionGetDataCommand { GameID = game.Id, EntityType = "MapModel" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Handle_NoFilters_UnknownEntityType_ReturnsEmptyList()
        {
            var game = BuildGame();
            var cmd = new ActionGetDataCommand { GameID = game.Id, EntityType = "SomeUnknownModel" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(result);
        }
    }
}
