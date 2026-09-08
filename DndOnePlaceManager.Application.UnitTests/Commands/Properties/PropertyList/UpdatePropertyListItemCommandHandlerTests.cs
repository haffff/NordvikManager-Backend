using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.PropertyList
{
    public class UpdatePropertyListItemCommandHandlerTests : HandlerTestBase
    {
        private UpdatePropertyListItemCommandHandler Handler() => new(Db, Mapper);

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
            var cmd = new UpdatePropertyListItemCommand { Player = Player(), PropertyId = Guid.NewGuid(), ItemId = "a1", Fields = new() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ItemNotFound_ThrowsResourceNotFoundException()
        {
            var (_, propId) = SeedGameWithListProperty(new() { new() { Id = "a1", Fields = new() { ["name"] = "First" } } });
            var cmd = new UpdatePropertyListItemCommand { Player = Player(), PropertyId = propId, ItemId = "missing", Fields = new() { ["name"] = "x" } };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUpdate_MergesSuppliedFieldsWithoutOverwritingOthers()
        {
            var (_, propId) = SeedGameWithListProperty(new()
            {
                new() { Id = "a1", Fields = new() { ["name"] = "Hunt", ["value"] = "1" } },
            });
            var cmd = new UpdatePropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                ItemId = "a1",
                Fields = new() { ["value"] = "2" },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Single(items!);
            // "name" wasn't supplied in this update — a partial setAttrs-style merge
            // must leave it exactly as it was, not wipe it.
            Assert.Equal("Hunt", items![0].Fields["name"]);
            Assert.Equal("2", items[0].Fields["value"]);
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var (_, propId) = SeedGameWithListProperty(new() { new() { Id = "a1", Fields = new() } });
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new UpdatePropertyListItemCommand { Player = new PlayerDTO { Id = strangerId }, PropertyId = propId, ItemId = "a1", Fields = new() };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
