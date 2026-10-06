using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // SQLite-backed with the real permission service: the handler used to load the whole
    // Properties table and run a permission query per property.
    public class GetPropertiesByQueryCommandHandlerTests : SqliteHandlerTestBase
    {
        private GetPropertiesByQueryCommandHandler Handler() => new(Db, Mapper, Permissions);

        private DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO Player() => new() { Id = PlayerId, Name = "Tester" };

        private readonly HashSet<Guid> readableParents = new();

        // Parents are readable by everyone unless the test says otherwise.
        private PropertyModel SeedProperty(Guid parentId, string name, string? value = "v",
            bool isProtected = false, bool readable = true)
        {
            var prop = new PropertyModel { Id = Guid.NewGuid(), ParentID = parentId, Name = name, Value = value, IsProtected = isProtected };
            using var seed = SeedContext();
            seed.Properties.Add(prop);
            if (readable && readableParents.Add(parentId))
                seed.Permissions.Add(new PermissionModel { ModelID = parentId, All = true, Permission = Permission.Read });
            seed.SaveChanges();
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
            SeedProperty(deniedParent, "Secret", readable: false);

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

        [Fact]
        public async Task Handle_SendsTheSameNumberOfQueriesHoweverManyMatches()
        {
            for (int i = 0; i < 30; i++)
            {
                var parent = Guid.NewGuid();
                SeedProperty(parent, "Name");
                SeedProperty(parent, "Unrelated");
            }
            Commands.Reset();
            var cmd = new GetPropertiesByQueryCommand { Player = Player(), PropertyNames = new[] { "Name" } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(30, result.Count);
            Assert.True(Commands.Count <= 2, $"expected at most 2 queries, got {Commands.Count}");
        }
    }
}
