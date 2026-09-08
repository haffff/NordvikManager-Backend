using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.PropertyList
{
    public class ReorderPropertyListCommandHandlerTests : HandlerTestBase
    {
        private ReorderPropertyListCommandHandler Handler() => new(Db, Mapper);

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
            var cmd = new ReorderPropertyListCommand { Player = Player(), PropertyId = Guid.NewGuid(), OrderedItemIds = new() { "a1" } };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidReorder_PreservesIdentityAndFieldsInNewOrder()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var cmd = new ReorderPropertyListCommand { Player = Player(), PropertyId = propId, OrderedItemIds = new() { "a3", "a1", "a2" } };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Equal(new[] { "a3", "a1", "a2" }, items!.Select(i => i.Id));
            // Fields travel with identity, not position — reorder must not re-create rows.
            Assert.Equal("Third", items[0].Fields["name"]);
            Assert.Equal("First", items[1].Fields["name"]);
            Assert.Equal("Second", items[2].Fields["name"]);
        }

        [Fact]
        public async Task Handle_OrderOmitsAnItem_AppendsItPreservingRelativeOrder()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            // a2 is left out of the requested order entirely.
            var cmd = new ReorderPropertyListCommand { Player = Player(), PropertyId = propId, OrderedItemIds = new() { "a3", "a1" } };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Equal(new[] { "a3", "a1", "a2" }, items!.Select(i => i.Id));
        }

        [Fact]
        public async Task Handle_OrderReferencesUnknownId_SkipsItSilently()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var cmd = new ReorderPropertyListCommand { Player = Player(), PropertyId = propId, OrderedItemIds = new() { "ghost", "a2", "a1", "a3" } };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Equal(new[] { "a2", "a1", "a3" }, items!.Select(i => i.Id));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var (_, propId) = SeedGameWithListProperty(new(ThreeItems));
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new ReorderPropertyListCommand { Player = new PlayerDTO { Id = strangerId }, PropertyId = propId, OrderedItemIds = new() { "a1" } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
