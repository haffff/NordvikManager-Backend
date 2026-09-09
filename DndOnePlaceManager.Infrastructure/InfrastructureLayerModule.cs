using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services;
using DndOnePlaceManager.Infrastructure.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Infrastructure
{
    public class InfrastructureLayerModule
    {
        /// <summary>
        /// Registers the provider-agnostic infrastructure services. The caller is now
        /// responsible for registering <c>IDbContext</c> (with its EF Core provider) —
        /// the relational providers moved to DNDOnePlaceManager so they stay out of the
        /// browser-wasm asset graph. See the .csproj comment.
        /// </summary>
        public static void Register(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>();
            services.AddScoped<IAddonRepositoryService, AddonRepositoryService>();
            services.AddScoped<IVersionService, VersionService>();
            services.AddHttpClient();
            services.AddScoped<IProxyHttpService, ProxyHttpService>();
        }
    }
}
