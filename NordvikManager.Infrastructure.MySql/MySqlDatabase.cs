using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Infrastructure.MySql
{
    /// <summary>
    /// MySQL for <c>InfrastructureLayerModule.Register</c>'s <c>useOtherDatabase</c>, used when
    /// <c>UseSqlite</c> is off.
    /// </summary>
    public static class MySqlDatabase
    {
        // Split queries as for SQLite: loading several collections of a game in one joined
        // query multiplies their row counts.
        public static void Use(DbContextOptionsBuilder options, string? connectionString)
            => options.UseMySQL(connectionString!, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }
}
