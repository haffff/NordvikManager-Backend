using NordvikManager.Application.Commands.Properties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using Newtonsoft.Json;

namespace NordvikManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    public class MoveCustomLayerCommandHandlerTests : HandlerTestBase
    {
        private AddCustomLayerCommandHandler AddHandler() => new(Db, Mapper);
        private MoveCustomLayerCommandHandler MoveHandler() => new(Db, Mapper);

        private static Dictionary<string, int> LayerIdsByName(PropertyDTO dto) =>
            JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto.Value!)!
                .ToDictionary(i => i.Fields["name"]!, i => int.Parse(i.Fields["layerId"]!));

        // A lone layer moved one step leaves its band for the next one; Grid and Token are
        // boundaries shared by two bands, not gaps to skip:
        // - down from Grid-Token lands in Map-Grid;
        // - up from Map-Grid lands in Grid-Token (the mirror image);
        // - up from Grid-Token lands in the top band, skipping the dead Token-TokenUi gap
        //   instead of bouncing back into Grid-Token.
        [Theory]
        [InlineData(CustomLayerLayout.GridLayerId, -1, CustomLayerLayout.MapLayerId, CustomLayerLayout.GridLayerId)]
        [InlineData(CustomLayerLayout.MapLayerId, 1, CustomLayerLayout.GridLayerId, CustomLayerLayout.TokenLayerId)]
        [InlineData(CustomLayerLayout.GridLayerId, 1, CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.TopBandCeiling)]
        public async Task Handle_Move_CrossesIntoTheNextBand(int addedAfter, int direction, int above, int below)
        {
            var game = BuildGame();

            var (_, addedDto) = await AddHandler().Handle(
                new AddCustomLayerCommand { Player = Player(), GameId = game.Id, Name = "L", AfterLayerId = addedAfter },
                CancellationToken.None);
            var itemId = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(addedDto!.Value!)!.Single().Id;

            var (response, movedDto) = await MoveHandler().Handle(
                new MoveCustomLayerCommand { Player = Player(), GameId = game.Id, ItemId = itemId, Direction = direction },
                CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var newLayerId = LayerIdsByName(movedDto!)["L"];
            Assert.True(newLayerId > above);
            Assert.True(newLayerId < below);
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
