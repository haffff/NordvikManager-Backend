using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    public class AddCustomLayerCommandHandlerTests : HandlerTestBase
    {
        private AddCustomLayerCommandHandler Handler() => new(Db, Mapper);

        private MapModel SeedMap(GameModel game)
        {
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = game, Elements = new(), Properties = new() };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        private ElementModel SeedElement(MapModel map, int layer) => SeedElementInDb(new ElementModel
        {
            Id = Guid.NewGuid(),
            Map = map,
            MapId = map.Id,
            Layer = layer,
            Details = new(),
        });

        private ElementModel SeedElementInDb(ElementModel element)
        {
            Db.Elements.Add(element);
            Db.SaveChanges();
            return element;
        }

        [Fact]
        public async Task Handle_NullAfterLayerId_InsertsAboveTokenUi()
        {
            var game = BuildGame();
            var cmd = new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Fog", AfterLayerId = null };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            var layerId = int.Parse(items!.Single().Fields["layerId"]!);
            Assert.True(layerId > CustomLayerLayout.TokenUiLayerId);
            Assert.True(layerId < CustomLayerLayout.TopBandCeiling);
        }

        [Fact]
        public async Task Handle_AfterGrid_LandsBetweenGridAndToken()
        {
            var game = BuildGame();
            var cmd = new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Fog", AfterLayerId = CustomLayerLayout.GridLayerId };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var layerId = int.Parse(JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!)!.Single().Fields["layerId"]!);
            Assert.True(layerId > CustomLayerLayout.GridLayerId);
            Assert.True(layerId < CustomLayerLayout.TokenLayerId);
        }

        [Fact]
        public async Task Handle_AfterMap_LandsBetweenMapAndGrid()
        {
            var game = BuildGame();
            var cmd = new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Water", AfterLayerId = CustomLayerLayout.MapLayerId };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var layerId = int.Parse(JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!)!.Single().Fields["layerId"]!);
            Assert.True(layerId > CustomLayerLayout.MapLayerId);
            Assert.True(layerId < CustomLayerLayout.GridLayerId);
        }

        // Pins the batch-reassignment-aliasing fix: inserting a second layer above an
        // existing one in the same band shifts BOTH layers' numeric values (renormalize
        // spaces every member of the band evenly), so the existing layer's elements
        // must land on its NEW value, not silently stay parked on the old one.
        [Fact]
        public async Task Handle_SecondInsertInSameBand_ReassignsExistingLayersElementsToItsNewValue()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var handler = Handler();

            var (_, dto1) = await handler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L1", AfterLayerId = null }, CancellationToken.None);
            var l1Original = int.Parse(JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto1!.Value!)!.Single().Fields["layerId"]!);

            var element = SeedElement(map, l1Original);

            var (_, dto2) = await handler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L2", AfterLayerId = null }, CancellationToken.None);
            var items2 = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto2!.Value!)!;
            var l1New = int.Parse(items2.First(i => i.Fields["name"] == "L1").Fields["layerId"]!);
            var l2New = int.Parse(items2.First(i => i.Fields["name"] == "L2").Fields["layerId"]!);

            Assert.NotEqual(l1Original, l1New); // sanity: this scenario actually exercises a shift
            Assert.True(l2New > l1New); // L2 was inserted above L1

            var reloaded = await SeedContext().Elements.FindAsync(element.Id);
            Assert.Equal(l1New, reloaded!.Layer);
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new AddCustomLayerCommand { Player = new PlayerDTO { Id = strangerId }, GameId = game.Id, Name = "Fog" };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new AddCustomLayerCommand { Player = Player(), GameId = Guid.NewGuid(), Name = "Fog" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
