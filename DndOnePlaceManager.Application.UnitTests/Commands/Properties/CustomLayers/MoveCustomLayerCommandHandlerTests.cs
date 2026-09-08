using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    public class MoveCustomLayerCommandHandlerTests : HandlerTestBase
    {
        private AddCustomLayerCommandHandler AddHandler() => new(Db, Mapper);
        private MoveCustomLayerCommandHandler MoveHandler() => new(Db, Mapper);

        private static Dictionary<string, int> LayerIdsByName(PropertyDTO dto) =>
            JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto.Value!)!
                .ToDictionary(i => i.Fields["name"]!, i => int.Parse(i.Fields["layerId"]!));

        [Fact]
        public async Task Handle_MoveDown_CrossesGridIntoMapGridBand()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            // Lands alone in the Grid->Token band.
            var (_, addedDto) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = CustomLayerLayout.GridLayerId },
                CancellationToken.None);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = -1 },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var newLayerId = LayerIdsByName(movedDto!)["L"];
            Assert.True(newLayerId > CustomLayerLayout.MapLayerId);
            Assert.True(newLayerId < CustomLayerLayout.GridLayerId);
        }

        // Grid is a genuine shared boundary (band1's Hi and band2's Lo simultaneously),
        // not a dead-gap skip — moving up out of the Map-Grid band must land in
        // Grid-Token, the mirror image of Handle_MoveDown_CrossesGridIntoMapGridBand.
        [Fact]
        public async Task Handle_MoveUp_CrossesGridIntoGridTokenBand()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            var (_, addedDto) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = CustomLayerLayout.MapLayerId },
                CancellationToken.None);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = 1 },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var newLayerId = LayerIdsByName(movedDto!)["L"];
            Assert.True(newLayerId > CustomLayerLayout.GridLayerId);
            Assert.True(newLayerId < CustomLayerLayout.TokenLayerId);
        }

        // Symmetric counterpart to the downward dead-gap regression below: moving UP
        // out of the Grid-Token band must land in the top band (skipping the dead
        // 100-110 gap upward), not bounce back into Grid-Token.
        [Fact]
        public async Task Handle_MoveUp_CrossesTokenIntoTopBand()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            var (_, addedDto) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = CustomLayerLayout.GridLayerId },
                CancellationToken.None);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = 1 },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var newLayerId = LayerIdsByName(movedDto!)["L"];
            Assert.True(newLayerId > CustomLayerLayout.TokenUiLayerId);
            Assert.True(newLayerId < CustomLayerLayout.TopBandCeiling);
        }

        [Fact]
        public async Task Handle_MoveUpWithinBand_ReordersRelativeToSibling()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            // Both land in the top band; "First" added first ends up below "Second".
            var (_, dto1) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "First", AfterLayerId = null }, CancellationToken.None);
            var firstId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto1!.Value!)!.Single().Id;

            var (_, dto2) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "Second", AfterLayerId = null }, CancellationToken.None);
            var beforeMove = LayerIdsByName(dto2!);
            Assert.True(beforeMove["First"] < beforeMove["Second"]);

            var (_, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = firstId, Direction = 1 },
                CancellationToken.None);

            var afterMove = LayerIdsByName(movedDto!);
            Assert.True(afterMove["First"] > afterMove["Second"]);
        }

        // Regression: a layer alone at the very top of the stack, moved down, must
        // land in the Grid-Token band (its new upper neighbor is Token) — not bounce
        // back into the top band it just left, which would make every subsequent
        // "move down" from the top a permanent no-op.
        [Fact]
        public async Task Handle_MoveDownFromSoleTopBandPosition_LandsInGridTokenBand()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            var (_, addedDto) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = null }, CancellationToken.None);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = -1 },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var newLayerId = LayerIdsByName(movedDto!)["L"];
            Assert.True(newLayerId > CustomLayerLayout.GridLayerId);
            Assert.True(newLayerId < CustomLayerLayout.TokenLayerId);

            // And it must be movable again afterward (the original bug made this a
            // permanent dead end — every further "down" click stayed in the top band).
            var (_, movedAgainDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = -1 },
                CancellationToken.None);
            var finalLayerId = LayerIdsByName(movedAgainDto!)["L"];
            Assert.True(finalLayerId > CustomLayerLayout.MapLayerId);
            Assert.True(finalLayerId < CustomLayerLayout.GridLayerId);
        }

        [Fact]
        public async Task Handle_MoveUpAtTopExtreme_IsNoOp()
        {
            var game = BuildGame();
            var addHandler = AddHandler();

            var (_, addedDto) = await addHandler.Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = null }, CancellationToken.None);
            var before = LayerIdsByName(addedDto!);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = 1 },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var after = LayerIdsByName(movedDto!);
            Assert.Equal(before["L"], after["L"]);
        }

        [Fact]
        public async Task Handle_ItemNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            await AddHandler().Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = null }, CancellationToken.None);

            var cmd = new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = "does-not-exist", Direction = 1 };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => MoveHandler().Handle(cmd, CancellationToken.None));
        }
    }
}
