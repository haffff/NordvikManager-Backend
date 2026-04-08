using DNDOnePlaceManager.Data.Contexts;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services;
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

            if (useSqlite)
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseSqlite(connectionString));
            else
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseMySQL(connectionString));

            services.AddScoped<IAddonRepositoryService, AddonRepositoryService>();
            services.AddScoped<IVersionService, VersionService>();
            services.AddHttpClient();
            services.AddScoped<IProxyHttpService, ProxyHttpService>();
        }
    }
}
