using DNDOnePlaceManager.Data.Contexts;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services;
using DndOnePlaceManager.Infrastructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Infrastructure
{
    public class InfrastructureLayerModule
    {
        public static void Register(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("ConnectionStrings")["DBData"];
            var useSqlite = configuration.GetValue<bool>("UseSqlite");

            // Split queries by default: loading several collections of a game in one joined
            // query multiplies their row counts (e.g. resources × tree entries ran into tens of
            // millions of rows and SQLite failed with "disk full" sorting them).
            if (useSqlite)
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseSqlite(connectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
            else
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseMySQL(connectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

            services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>();
            services.AddScoped<IAddonRepositoryService, AddonRepositoryService>();
            services.AddScoped<IVersionService, VersionService>();
            services.AddHttpClient();
            services.AddScoped<IProxyHttpService, ProxyHttpService>();
        }
    }
}
