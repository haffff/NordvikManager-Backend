using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    // The layer handlers need the game's "customLayers" property only; they used to load every
    // property of the game.
    public class CustomLayerLoadingTests : HandlerTestBase
    {
        private readonly GameModel game;

        public CustomLayerLoadingTests()
        {
            game = BuildGame();
            for (int i = 0; i < 10; i++)
                Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = $"setting{i}", Value = "v", ParentID = game.Id, EntityName = "GameModel", Game = game });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        private void AssertOnlyTheLayerListLoaded() =>
            Assert.Equal("customLayers", Assert.Single(Db.ChangeTracker.Entries<PropertyModel>()).Entity.Name);

        private async Task<string> AddLayer(string name)
        {
            var (_, dto) = await new AddCustomLayerCommandHandler(Db, Mapper).Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = name }, CancellationToken.None);
            return JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!)!.Single(x => x.Fields["name"] == name).Id;
        }

        [Fact]
        public async Task Add_LoadsOnlyTheLayerList()
        {
            await AddLayer("Fog");
            AssertOnlyTheLayerListLoaded();

            Db.ChangeTracker.Clear();
            await AddLayer("Rain"); // second add finds the existing list
            AssertOnlyTheLayerListLoaded();
        }

        [Fact]
        public async Task Move_LoadsOnlyTheLayerList()
        {
            await AddLayer("Fog");
            var rain = await AddLayer("Rain");
            Db.ChangeTracker.Clear();

            await new MoveCustomLayerCommandHandler(Db, Mapper).Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = rain, Direction = -1 }, CancellationToken.None);

            AssertOnlyTheLayerListLoaded();
        }

        [Fact]
        public async Task Remove_LoadsOnlyTheLayerList()
        {
            var fog = await AddLayer("Fog");
            Db.ChangeTracker.Clear();

            await new RemoveCustomLayerCommandHandler(Db, Mapper).Handle(
                new RemoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = fog }, CancellationToken.None);

            AssertOnlyTheLayerListLoaded();
        }
    }
}
