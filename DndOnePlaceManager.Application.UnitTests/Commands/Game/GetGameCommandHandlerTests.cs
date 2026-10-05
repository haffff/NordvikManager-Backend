using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.BattleMap.GetGame;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Game
{
    public class GetGameCommandHandlerTests : HandlerTestBase
    {
        private GetGameCommandHandler Handler() => new(Db, Mapper);

        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid mapId = Guid.NewGuid();

        public GetGameCommandHandlerTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId,
                Name = "Campaign",
                MasterId = PlayerId,
                SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
                Properties = new List<PropertyModel> { new PropertyModel { Id = Guid.NewGuid(), Name = "disallowPlayerLayouts", Value = "true" } },
                Layouts = new List<LayoutModel> { new LayoutModel { Id = Guid.NewGuid(), Name = "Main", Value = "{}", Default = true } },
                Maps = new List<MapModel>
                {
                    new MapModel
                    {
                        Id = mapId, Name = "Dungeon", Path = "maps/dungeon",
                        Elements = Enumerable.Range(0, 3).Select(i => new ElementModel
                        {
                            Id = Guid.NewGuid(),
                            Properties = new List<PropertyModel> { new PropertyModel { Id = Guid.NewGuid(), Name = $"hp{i}", Value = "10" } },
                        }).ToList(),
                    },
                },
            });
            seed.SaveChanges();
        }

        [Fact]
        public async Task Handle_ReturnsMapsLayoutsAndSettings()
        {
            var result = await Handler().Handle(new GetGameCommand { GameID = gameId, PlayerID = PlayerId }, CancellationToken.None);

            var map = Assert.Single(result.Maps!);
            Assert.Equal(mapId, map.Id);
            Assert.Equal("Dungeon", map.Name);
            Assert.Equal("maps/dungeon", map.Path);
            Assert.Equal("Main", result.DefaultLayout!.Name);
            Assert.True(result.DisallowPlayerLayouts);
            Assert.Equal(PlayerId, result.Master!.Id);
        }

        [Fact]
        public async Task Handle_DoesNotLoadMapElementsOrTheirProperties()
        {
            await Handler().Handle(new GetGameCommand { GameID = gameId, PlayerID = PlayerId }, CancellationToken.None);

            Assert.Empty(Db.ChangeTracker.Entries<ElementModel>());
            // Only the game's own setting is loaded, not the elements' properties.
            Assert.Single(Db.ChangeTracker.Entries<PropertyModel>());
        }
    }
}
