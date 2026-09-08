using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.PropertyList
{
    public class AddPropertyListItemCommandHandlerTests : HandlerTestBase
    {
        private AddPropertyListItemCommandHandler Handler() => new(Db, Mapper);

        private (GameModel game, Guid propertyId) SeedGameWithListProperty(string? value = "[]")
        {
            var game = BuildGame();
            var propId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel
            {
                Id = propId,
                Name = "Clocks",
                Value = value,
                ParentID = game.Id,
                EntityName = "GameModel",
            });
            Db.SaveChanges();
            return (game, propId);
        }

        [Fact]
        public async Task Handle_PropertyNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = Guid.NewGuid(),
                Fields = new() { ["name"] = "Hunt" },
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidAdd_AppendsItemWithGeneratedId()
        {
            var (_, propId) = SeedGameWithListProperty();
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                Fields = new() { ["name"] = "Hunt", ["value"] = "2" },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Single(items!);
            Assert.False(string.IsNullOrEmpty(items![0].Id));
            Assert.Equal("Hunt", items[0].Fields["name"]);
            Assert.Equal("2", items[0].Fields["value"]);
        }

        [Fact]
        public async Task Handle_SecondAdd_AppendsWithoutDisturbingExistingItem()
        {
            var existing = new PropertyListItemDTO { Id = "a1", Fields = new() { ["name"] = "Old" } };
            var (_, propId) = SeedGameWithListProperty(JsonConvert.SerializeObject(new List<PropertyListItemDTO> { existing }));
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                Fields = new() { ["name"] = "New" },
            };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Equal(2, items!.Count);
            Assert.Equal("a1", items[0].Id);
            Assert.Equal("Old", items[0].Fields["name"]);
            Assert.NotEqual("a1", items[1].Id);
            Assert.Equal("New", items[1].Fields["name"]);
        }

        [Fact]
        public async Task Handle_ClientSuppliedItemId_UsesThatIdInsteadOfGenerating()
        {
            var (_, propId) = SeedGameWithListProperty();
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                Fields = new() { ["name"] = "Hunt" },
                ItemId = "local-row-1",
            };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            var items = JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(dto!.Value!);
            Assert.Single(items!);
            Assert.Equal("local-row-1", items![0].Id);
        }

        [Fact]
        public async Task Handle_MalformedClientSuppliedItemId_ThrowsWrongArgumentsException()
        {
            var (_, propId) = SeedGameWithListProperty();
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                Fields = new() { ["name"] = "New" },
                ItemId = "not a valid id; has spaces and ; punctuation",
            };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DuplicateClientSuppliedItemId_ThrowsWrongArgumentsException()
        {
            var existing = new PropertyListItemDTO { Id = "a1", Fields = new() { ["name"] = "Old" } };
            var (_, propId) = SeedGameWithListProperty(JsonConvert.SerializeObject(new List<PropertyListItemDTO> { existing }));
            var cmd = new AddPropertyListItemCommand
            {
                Player = Player(),
                PropertyId = propId,
                Fields = new() { ["name"] = "New" },
                ItemId = "a1",
            };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var (_, propId) = SeedGameWithListProperty();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new AddPropertyListItemCommand
            {
                Player = new PlayerDTO { Id = strangerId },
                PropertyId = propId,
                Fields = new(),
            };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
