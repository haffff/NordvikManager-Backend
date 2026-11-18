using NordvikManager.Application.Commands.Addons.GetAddons;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using ActionModel = NordvikManager.Domain.Entities.BattleMap.ActionModel;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // The installed list (Addons panel) on a real SQLite database: the query that finds
    // addons with outside requests must translate there (InMemory runs any query).
    public class GetAddonsSqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();

        public GetAddonsSqliteTests()
        {
            using var seed = SeedContext();
            var fetch = new ActionModel { Id = Guid.NewGuid(), Name = "Fetch", Prefix = "", Content = "[{\"Type\":\"SendRequest\",\"Data\":{}}]" };
            var say = new ActionModel { Id = Guid.NewGuid(), Name = "Say", Prefix = "", Content = "[{\"Type\":\"SendChat\",\"Data\":{\"Message\":\"SendRequest\"}}]" };
            seed.Games.Add(new GameModel
            {
                Actions = new List<ActionModel> { fetch, say },
                Id = gameId, Name = "Campaign", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
                Addons = new List<AddonModel>
                {
                    new AddonModel
                    {
                        Id = Guid.NewGuid(), Name = "Sync", Key = "sync",
                        Actions = new List<ActionModel> { fetch },
                    },
                    new AddonModel
                    {
                        Id = Guid.NewGuid(), Name = "Chat", Key = "chat",
                        Actions = new List<ActionModel> { say },
                    },
                },
            });
            seed.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = PlayerId, Permission = Permission.All });
            seed.SaveChanges();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Handle_ListsTheGamesAddons_WithTheirOutsideConnections(bool flat)
        {
            var result = await new GetAddonsCommandHandler(Db, Mapper).Handle(
                new GetAddonsCommand { GameId = gameId, Player = new() { Id = PlayerId, Name = "GM" }, Flat = flat },
                CancellationToken.None);

            Assert.True(result.Single(a => a.Key == "sync").UsesOutsideConnections);
            Assert.False(result.Single(a => a.Key == "chat").UsesOutsideConnections);
        }
    }
}
