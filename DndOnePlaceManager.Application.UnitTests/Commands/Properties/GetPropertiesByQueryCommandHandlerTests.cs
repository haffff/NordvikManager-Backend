using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    public class GetPropertiesByQueryCommandHandlerTests : HandlerTestBase
    {
        public GetPropertiesByQueryCommandHandlerTests()
        {
            // This handler uses the (PlayerDTO, Guid modelId, Permission) overload — not
            // covered by HandlerTestBase's default (Guid, IEntity, Permission) setup — so
            // grant it explicitly here.
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<PlayerDTO>(), It.IsAny<Guid>(), It.IsAny<Permission>()))
                .Returns(true);
        }

        private GetPropertiesByQueryCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object);

        private PropertyModel SeedProperty(Guid parentId, string name, string? value = "v",
            bool isProtected = false)
        {
            var prop = new PropertyModel { Id = Guid.NewGuid(), ParentID = parentId, Name = name, Value = value, IsProtected = isProtected };
            Db.Properties.Add(prop);
            Db.SaveChanges();
            return prop;
        }

        [Fact]
        public async Task Handle_NoFilters_ReturnsAllReadableProperties()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Handle_FilterByParentIds_ReturnsOnlyMatchingParent()
        {
            var wantedParent = Guid.NewGuid();
            var otherParent = Guid.NewGuid();
            SeedProperty(wantedParent, "Name");
            SeedProperty(otherParent, "Name");
            var cmd = new GetPropertiesByQueryCommand { Player = Player(), ParentIDs = new[] { wantedParent } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(wantedParent, result[0].ParentID);
        }

        [Fact]
        public async Task Handle_FilterByIds_ReturnsOnlyMatchingIds()
        {
            var parentId = Guid.NewGuid();
            var wanted = SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { Player = Player(), Ids = new[] { wanted.Id } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(wanted.Id, result[0].Id);
        }

        [Fact]
        public async Task Handle_FilterByPropertyNames_ReturnsOnlyMatchingNames()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { Player = Player(), PropertyNames = new[] { "Name" } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Name", result[0].Name);
        }

        [Fact]
        public async Task Handle_FilterByPrefix_ReturnsOnlyMatchingPrefix()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "img_main");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { Player = Player(), Prefix = "img_" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("img_main", result[0].Name);
        }

        [Fact]
        public async Task Handle_NoReadPermissionOnParent_ExcludesThoseProperties()
        {
            var readableParent = Guid.NewGuid();
            var deniedParent = Guid.NewGuid();
            SeedProperty(readableParent, "Name");
            SeedProperty(deniedParent, "Secret");

            PermissionsMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<PlayerDTO>(), deniedParent, It.IsAny<Permission>()))
                .Returns(false);

            var cmd = new GetPropertiesByQueryCommand { Player = Player() };
            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Name", result[0].Name);
        }

        [Fact]
        public async Task Handle_ProtectedProperty_NullsOutValueInResult()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Secret", value: "hidden", isProtected: true);
            var cmd = new GetPropertiesByQueryCommand { Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Null(result[0].Value);
        }

        [Fact]
        public async Task Handle_UnprotectedProperty_KeepsValueInResult()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Open", value: "visible", isProtected: false);
            var cmd = new GetPropertiesByQueryCommand { Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("visible", result[0].Value);
        }
    }
}
