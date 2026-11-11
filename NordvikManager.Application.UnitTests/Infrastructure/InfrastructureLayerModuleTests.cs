using NordvikManager.Infrastructure;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NordvikManager.Application.UnitTests.Infrastructure
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

        private static IConfiguration NotSqlite() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "false",
                ["ConnectionStrings:DBData"] = "server=unused",
            })
            .Build();

        // Infrastructure carries only SQLite. Any other database (MySQL natively, an in-memory or
        // browser database in a wasm host) comes from the host, so its packages stay out of here.
        [Fact]
        public void Register_NotSqlite_UsesTheHostsDatabase()
        {
            var services = new ServiceCollection();
            string? givenConnectionString = null;
            InfrastructureLayerModule.Register(services, NotSqlite(), (options, connectionString) =>
            {
                givenConnectionString = connectionString;
                options.UseInMemoryDatabase("host-db");
            });

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService<IDbContext>();

            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
            Assert.Equal("server=unused", givenConnectionString);
        }

        [Fact]
        public void Register_NotSqlite_WithoutAHostDatabase_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => InfrastructureLayerModule.Register(new ServiceCollection(), NotSqlite()));
            Assert.Contains("UseSqlite", ex.Message);
        }
    }
}
