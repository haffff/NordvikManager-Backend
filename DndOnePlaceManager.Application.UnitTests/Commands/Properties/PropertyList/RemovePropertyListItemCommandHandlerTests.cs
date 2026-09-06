using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.PropertyList
{
    public class RemovePropertyListItemCommandHandlerTests : HandlerTestBase
    {
        private RemovePropertyListItemCommandHandler Handler() => new(Db, Mapper);

        private static readonly List<PropertyListItemDTO> ThreeItems = new()
        {
            new() { Id = "a1", Fields = new() { ["name"] = "First" } },
            new() { Id = "a2", Fields = new() { ["name"] = "Second" } },
            new() { Id = "a3", Fields = new() { ["name"] = "Third" } },
        };

        private (GameModel game, Guid propertyId) SeedGameWithListProperty(List<PropertyListItemDTO> items)
        {
            var game = BuildGame();
            var propId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel
            {
                Id = propId,
                Name = "Clocks",
                Value = JsonConvert.SerializeObject(items),
                ParentID = game.Id,
                EntityName = "GameModel",
            });
            Db.SaveChanges();
            return (game, propId);
        }

        [Fact]
        public async Task Handle_PropertyNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new RemovePropertyListItemCommand { Player = Player(), PropertyId = Guid.NewGuid(), ItemId = "a1" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ItemNotFound_ThrowsResourceNotFoundException()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var cmd = new RemovePropertyListItemCommand { Player = Player(), PropertyId = propId, ItemId = "does-not-exist" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidRemoval_RemovesOnlyTargetItem_NoPositionalShiftOnSurvivors()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var cmd = new RemovePropertyListItemCommand { Player = Player(), PropertyId = propId, ItemId = "a2" };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Equal(2, items!.Count);
            // Identity and fields of the survivors are untouched — no reassigned ids,
            // no shifted field values (the old name_0..N-1 scheme's failure mode).
            Assert.Equal("a1", items[0].Id);
            Assert.Equal("First", items[0].Fields["name"]);
            Assert.Equal("a3", items[1].Id);
            Assert.Equal("Third", items[1].Fields["name"]);
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new RemovePropertyListItemCommand { Player = new PlayerDTO { Id = strangerId }, PropertyId = propId, ItemId = "a1" };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
