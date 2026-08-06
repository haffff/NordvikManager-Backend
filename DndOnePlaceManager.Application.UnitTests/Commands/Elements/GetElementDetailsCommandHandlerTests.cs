using DndOnePlaceManager.Application.Commands.Elements.GetElementDetails;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Elements
{
    public class GetElementDetailsCommandHandlerTests : HandlerTestBase
    {
        private GetElementDetailsCommandHandler Handler() => new(Db, Mapper);

        private ElementModel SeedElement(params (string Key, string Value, string Type)[] details)
        {
            var element = new ElementModel { Id = Guid.NewGuid(), Selectable = true, Details = new(), Properties = new() };
            Db.Elements.Add(element);
            Db.SaveChanges();
            foreach (var (key, value, type) in details)
            {
                Db.Set<ElementDetailModel>().Add(new ElementDetailModel { Id = Guid.NewGuid(), ElementId = element.Id, Key = key, Value = value, Type = type });
            }
            Db.SaveChanges();
            return element;
        }

        [Fact]
        public async Task Handle_ElementNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new GetElementDetailsCommand { ElementId = Guid.NewGuid(), Name = "width", Player = Player() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var element = SeedElement(("width", "100", "Integer"));
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read))
                .Returns(false);
            var cmd = new GetElementDetailsCommand { ElementId = element.Id, Name = "width", Player = new PlayerDTO { Id = strangerId } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DetailNotFound_ThrowsResourceNotFoundException()
        {
            var element = SeedElement(("width", "100", "Integer"));
            var cmd = new GetElementDetailsCommand { ElementId = element.Id, Name = "height", Player = Player() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_StringDetail_ReturnsRawValue()
        {
            var element = SeedElement(("label", "Goblin", "String"));
            var cmd = new GetElementDetailsCommand { ElementId = element.Id, Name = "label", Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Goblin", result["label"]);
        }

        [Fact]
        public async Task Handle_NumberDetail_ReturnsParsedFloat()
        {
            // Whole number to avoid decimal-separator culture issues (float.Parse uses CurrentCulture).
            var element = SeedElement(("width", "12", "Number"));
            var cmd = new GetElementDetailsCommand { ElementId = element.Id, Name = "width", Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(12f, result["width"]);
        }

        [Fact]
        public async Task Handle_NameMatchIsCaseInsensitive()
        {
            var element = SeedElement(("Width", "100", "Integer"));
            var cmd = new GetElementDetailsCommand { ElementId = element.Id, Name = "width", Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(result.ContainsKey("Width"));
        }
    }
}
