using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

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

        // GetEntitiesList used Games.Find(id)?.Maps without loading Maps, so this only worked
        // when the maps happened to be tracked already (as they were in the test above).
        [Fact]
        public async Task Handle_ByProperty_FindsEntitiesThatAreNotLoadedYet()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Dungeon");
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), EntityName = "MapModel", Name = "Difficulty", Value = "Hard", ParentID = map.Id });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var result = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = "MapModel", Property = "Difficulty" }, CancellationToken.None);

            Assert.Equal("Dungeon", ((MapDTO)Assert.Single(result)).Name);
        }

        [Fact]
        public async Task Handle_ByName_LoadsOnlyTheMatchingEntitiesOfThatType()
        {
            var game = BuildGame();
            SeedMap(game, "Dungeon");
            foreach (var name in new[] { "Goblin", "Orc", "Troll" })
                Db.Cards.Add(new CardModel { Id = Guid.NewGuid(), Name = name, GameId = game.Id, Properties = new List<PropertyModel>() });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var result = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = "CardModel", Name = "Orc" }, CancellationToken.None);

            Assert.Equal("Orc", ((CardDto)Assert.Single(result)).Name);
            Assert.Single(Db.ChangeTracker.Entries<CardModel>());
            Assert.Empty(Db.ChangeTracker.Entries<MapModel>());
        }

        [Fact]
        public async Task Handle_NoFilters_ElementModel_ReturnsElementsOfThisGamesMapsOnly()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Dungeon");
            var element = new ElementModel { Id = Guid.NewGuid(), Map = map };
            Db.Elements.Add(element);
            var other = new GameModel { Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            Db.Games.Add(other);
            var otherMap = SeedMap(other, "Elsewhere");
            Db.Elements.Add(new ElementModel { Id = Guid.NewGuid(), Map = otherMap });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var result = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = "ElementModel" }, CancellationToken.None);

            Assert.Equal(element.Id, ((ElementDTO)Assert.Single(result)).Id);
        }

        // By-ID lookups used dbContext.Find with no game check, so an action could read any
        // game's entity by its id.
        [Theory]
        [InlineData("MapModel")]
        [InlineData("ElementModel")]
        [InlineData("GameModel")]
        public async Task Handle_ById_EntityOfAnotherGame_ReturnsNothing(string type)
        {
            var game = BuildGame();
            var other = new GameModel { Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            Db.Games.Add(other);
            var otherMap = SeedMap(other, "Elsewhere");
            var otherElement = new ElementModel { Id = Guid.NewGuid(), Map = otherMap };
            Db.Elements.Add(otherElement);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            var id = type switch { "MapModel" => otherMap.Id, "ElementModel" => otherElement.Id, _ => other.Id };

            var result = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = type, ID = id }, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_ById_OwnElement_StillFound()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Dungeon");
            var element = new ElementModel { Id = Guid.NewGuid(), Map = map };
            Db.Elements.Add(element);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var byElement = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = "ElementModel", ID = element.Id }, CancellationToken.None);

            Assert.Equal(element.Id, ((ElementDTO)Assert.Single(byElement)).Id);
        }

        // The step's description used to suggest "Map", "Card"..., which matched nothing:
        // the lookup only knew "MapModel" etc. Both forms now work, in any case.
        [Theory]
        [InlineData("Map")]
        [InlineData("map")]
        [InlineData("MapModel")]
        [InlineData("mapmodel")]
        public async Task Handle_ShortOrModelTypeName_FindsMaps(string entityType)
        {
            var game = BuildGame();
            SeedMap(game, "Default");

            var result = await Handler().Handle(new ActionGetDataCommand { GameID = game.Id, EntityType = entityType, Name = "Default" }, CancellationToken.None);

            Assert.Equal("Default", ((MapDTO)Assert.Single(result)).Name);
        }
    }
}
