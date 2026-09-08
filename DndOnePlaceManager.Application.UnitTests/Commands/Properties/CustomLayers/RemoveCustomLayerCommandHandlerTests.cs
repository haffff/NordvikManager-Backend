using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    public class RemoveCustomLayerCommandHandlerTests : HandlerTestBase
    {
        private AddCustomLayerCommandHandler AddHandler() => new(Db, Mapper);
        private RemoveCustomLayerCommandHandler RemoveHandler() => new(Db, Mapper);

        private MapModel SeedMap(GameModel game)
        {
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = game, Elements = new(), Properties = new() };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        private ElementModel SeedElement(MapModel map, int layer)
        {
            var element = new ElementModel { Id = Guid.NewGuid(), Map = map, MapId = map.Id, Layer = layer, Details = new() };
            Db.Elements.Add(element);
            Db.SaveChanges();
            return element;
        }

        [Fact]
        public async Task Handle_ValidRemove_DeletesRowAndReassignsElementsToMap()
        {
            var game = BuildGame();
            var map = SeedMap(game);

            var (_, addedDto) = await AddHandler().Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Fog", AfterLayerId = null }, CancellationToken.None);
            var item = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single();
            var layerId = int.Parse(item.Fields["layerId"]!);
            var element = SeedElement(map, layerId);

            var (response, removedDto) = await RemoveHandler().Handle(
                new RemoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = item.Id }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Empty(JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(removedDto!.Value!)!);

            var reloaded = await SeedContext().Elements.FindAsync(element.Id);
            Assert.Equal(CustomLayerLayout.MapLayerId, reloaded!.Layer);
        }

        [Fact]
        public async Task Handle_ItemNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            await AddHandler().Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Fog", AfterLayerId = null }, CancellationToken.None);

            var cmd = new RemoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = "does-not-exist" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => RemoveHandler().Handle(cmd, CancellationToken.None));
        }
    }
}
