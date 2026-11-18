using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using ActionModel = NordvikManager.Domain.Entities.BattleMap.ActionModel;

namespace NordvikManager.Application.UnitTests.Commands.Properties
{
    // The addon network check on a real SQLite database: its query must translate there
    // (InMemory runs any query), for an action, its callers, and an action of no addon.
    public class ProxySupportSqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid declaring = Guid.NewGuid();
        private readonly Guid undeclared = Guid.NewGuid();
        private readonly Guid gmMade = Guid.NewGuid();

        public ProxySupportSqliteTests()
        {
            using var seed = SeedContext();
            ActionModel Action(Guid id) => new() { Id = id, Name = id.ToString(), Prefix = "", Content = "[]" };
            var a = Action(declaring);
            var b = Action(undeclared);
            seed.Games.Add(new GameModel
            {
                Id = Guid.NewGuid(), Name = "Campaign", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
                Actions = new List<ActionModel> { a, b, Action(gmMade) },
                Addons = new List<AddonModel>
                {
                    new AddonModel { Id = Guid.NewGuid(), Name = "Sync", Key = "sync", Network = new AddonNetwork { Hosts = new() { "https://api.example.com" } }, Actions = new() { a } },
                    new AddonModel { Id = Guid.NewGuid(), Name = "Old", Key = "old", Actions = new() { b } },
                },
            });
            seed.SaveChanges();
        }

        [Theory]
        [InlineData("declaring", null, "https://api.example.com/x", true)]
        [InlineData("declaring", null, "https://other.example.com/x", false)]
        [InlineData("undeclared", null, "https://other.example.com/x", true)]
        [InlineData("gmMade", "declaring", "https://other.example.com/x", false)]
        [InlineData("gmMade", "declaring", "https://api.example.com/x", true)]
        public async Task IsAllowedByAddonAsync_TranslatesOnSqlite(string action, string? caller, string url, bool allowed)
        {
            Guid Id(string name) => name switch { "declaring" => declaring, "undeclared" => undeclared, _ => gmMade };
            var callers = caller == null ? null : new List<Guid> { Id(caller) };

            Assert.Equal(allowed, await ProxySupport.IsAllowedByAddonAsync(Db, Id(action), callers, url, CancellationToken.None));
        }
    }
}
