using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NordvikManager.Infrastructure.MySql;
using System.Linq;
using Xunit;

namespace NordvikManager.Tests.Hosting
{
    // The MySQL adapter the native server passes to InfrastructureLayerModule.Register. Only the
    // options are checked: DndOneContext's constructor connects to MySQL (EnsureCreated).
    public class MySqlDatabaseTests
    {
        [Fact]
        public void Use_SetsUpMySql_ThatSplitsQueries()
        {
            var options = new DbContextOptionsBuilder();

            MySqlDatabase.Use(options, "server=localhost;database=nordvik;user=u;password=p");

            var relational = RelationalOptionsExtension.Extract(options.Options);
            Assert.Contains("MySQL", relational.GetType().Name);
            Assert.Equal("server=localhost;database=nordvik;user=u;password=p", relational.ConnectionString);
            Assert.Equal(QuerySplittingBehavior.SplitQuery, relational.QuerySplittingBehavior);
        }
    }
}
