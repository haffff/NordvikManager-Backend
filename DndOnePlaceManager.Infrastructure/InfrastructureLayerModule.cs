using DndOnePlaceManager.Infrastructure.Data.Contexts;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services;
using DNDOnePlaceManager.Data.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Infrastructure
{
    public class InfrastructureLayerModule
    {
        public static void Register(IServiceCollection services, IConfiguration configuration, IdentityBuilder identityBuilder)
        {
            var connectionString = configuration.GetSection("ConnectionStrings")["DBData"];
            var authConnectionString = configuration.GetSection("ConnectionStrings")["AuthData"];

            var useSqlite = configuration.GetValue<bool>("UseSqlite");
            if (useSqlite)
            {
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseSqlite(connectionString));
                services.AddDbContext<IAuthDBContext, AuthContext>(options => options.UseSqlite(authConnectionString));
            }
            else
            {
                services.AddDbContext<IDbContext, DndOneContext>(options => options.UseMySQL(connectionString));
                services.AddDbContext<IAuthDBContext, AuthContext>(options => options.UseMySQL(authConnectionString));
            }

            services.AddScoped<IAddonRepositoryService, AddonRepositoryService>();
            services.AddScoped<IVersionService, VersionService>();

            identityBuilder
                .AddEntityFrameworkStores<AuthContext>();
        }
    }
}