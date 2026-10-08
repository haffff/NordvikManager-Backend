using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Services
{
    public class PermissionsServiceTests : SqliteHandlerTestBase
    {
        private readonly Guid own = Guid.NewGuid();          // player row with Read
        private readonly Guid ownDenied = Guid.NewGuid();    // player row without Read, "everyone" row with Read — player row wins
        private readonly Guid everyone = Guid.NewGuid();     // only an "everyone" row with Read
        private readonly Guid editOnly = Guid.NewGuid();     // player row with Edit but not Read
        private readonly Guid nothing = Guid.NewGuid();      // no rows at all
        private readonly Guid otherPlayer = Guid.NewGuid();  // only another player's row

        public PermissionsServiceTests()
        {
            using var seed = SeedContext();
            seed.Permissions.AddRange(
                new PermissionModel { ModelID = own, PlayerID = PlayerId, Permission = Permission.Read | Permission.Edit },
                new PermissionModel { ModelID = ownDenied, PlayerID = PlayerId, Permission = Permission.None },
                new PermissionModel { ModelID = ownDenied, All = true, Permission = Permission.Read },
                new PermissionModel { ModelID = everyone, All = true, Permission = Permission.Read },
                new PermissionModel { ModelID = editOnly, PlayerID = PlayerId, Permission = Permission.Edit },
                new PermissionModel { ModelID = otherPlayer, PlayerID = Guid.NewGuid(), Permission = Permission.All });
            seed.SaveChanges();
        }

        private Guid[] AllIds => new[] { own, ownDenied, everyone, editOnly, nothing, otherPlayer };

        [Fact]
        public void GetPermittedIds_FollowsTheSameRulesAsSingleChecks()
        {
            var permitted = Permissions.GetPermittedIds(PlayerId, AllIds, Permission.Read);

            Assert.Equal(new HashSet<Guid> { own, everyone }, permitted);
            foreach (var id in AllIds)
                Assert.Equal(Permissions.CheckIfHasPermissions(PlayerId, Card(id), Permission.Read), permitted.Contains(id));
        }

        [Fact]
        public void GetPermittedIds_ChecksTheRequestedFlag()
        {
            Assert.Equal(new HashSet<Guid> { own, editOnly }, Permissions.GetPermittedIds(PlayerId, AllIds, Permission.Edit));
        }

        [Fact]
        public void GetPermittedIds_SendsOneQueryHoweverManyIds()
        {
            var many = AllIds.Concat(Enumerable.Range(0, 200).Select(_ => Guid.NewGuid())).ToArray();
            Commands.Reset();

            Permissions.GetPermittedIds(PlayerId, many, Permission.Read);

            Assert.Equal(1, Commands.Count);
        }

        [Fact]
        public void WithPermission_FiltersWithOneQuery()
        {
            var cards = AllIds.Select(Card).ToList();
            Commands.Reset();

            var visible = cards.WithPermission(PlayerId).Select(x => x.Id).ToHashSet();

            Assert.Equal(new HashSet<Guid> { own, everyone }, visible);
            Assert.Equal(1, Commands.Count);
        }

        [Fact]
        public void WithPermission_KeepsTheOrder()
        {
            var cards = new[] { everyone, nothing, own }.Select(Card).ToList();

            Assert.Equal(new[] { everyone, own }, cards.WithPermission(PlayerId).Select(x => x.Id));
        }

        private static CardModel Card(Guid id) => new() { Id = id, Name = id.ToString() };
    }
}
