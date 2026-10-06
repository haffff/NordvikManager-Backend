using DndOnePlaceManager.Infrastructure;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure
{
    public class InfrastructureLayerModuleTests
    {
        // Loading a game with several collections (players, maps, resources, tree entries, ...)
        // as one joined query multiplies their row counts. Split queries load each collection
        // separately, so a query that forgets AsSplitQuery() can't blow up.
        [Fact]
        public void Register_Sqlite_SplitsQueriesByDefault()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseSqlite"] = "true",
                    ["ConnectionStrings:DBData"] = "Data Source=:memory:",
                })
                .Build();
            var services = new ServiceCollection();
            InfrastructureLayerModule.Register(services, configuration);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService<IDbContext>();

            var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());
            Assert.Equal(QuerySplittingBehavior.SplitQuery, relational.QuerySplittingBehavior);
        }
    }
}
