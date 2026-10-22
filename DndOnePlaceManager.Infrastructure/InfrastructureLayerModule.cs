using DNDOnePlaceManager.Data.Contexts;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services;
using DndOnePlaceManager.Infrastructure.Services.Backup;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(BackupOptions.FromConfiguration(configuration));
            services.AddSingleton<IBackupService>(sp => new BackupService(sp.GetRequiredService<BackupOptions>(), sp.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IBackupScheduleStore, BackupScheduleStore>();
            services.AddSingleton<ScheduledBackupRunner>();
            services.AddScoped<IAddonRepositoryService, AddonRepositoryService>();
            services.AddScoped<IVersionService, VersionService>();
            services.AddHttpClient();
            services.AddSingleton(sp =>
            {
                var proxy = ProxyOptions.FromConfiguration(configuration);
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<ProxyOptions>();
                foreach (var entry in proxy.Ignored)
                    logger?.LogWarning(ProxyOptions.ConfigKey + ": ignoring \"{Entry}\" — use a host or host:port, e.g. localhost:37840", entry);
                return proxy;
            });
            services.AddProxyHttpClient();
            services.AddScoped<IProxyHttpService, ProxyHttpService>();
        }
    }
}
