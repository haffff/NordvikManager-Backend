using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NordvikManager.Api;
using NordvikManager.Application;
using NordvikManager.Infrastructure;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser-wasm GM host's services: the same layers as the native server
    /// (<see cref="InfrastructureLayerModule"/>, <see cref="ApplicationLayerModule"/>,
    /// <see cref="RealTimeLayerModule"/>), with the browser's own WebRTC, the host's own Central
    /// tokens, WebSocket-only signaling, and a SQLite file (on OPFS in the browser host), or an
    /// in-memory database.
    /// </summary>
    public static class WasmHost
    {
        /// <summary>The host's settings; <paramref name="overrides"/> replace any of them.</summary>
        /// <param name="databasePath">
        /// A SQLite file (as the native server uses), migrated when the host is built; without one,
        /// an in-memory database that lasts as long as the host.
        /// </param>
        public static IConfiguration Configuration(string centralServerUrl, IDictionary<string, string?>? overrides = null, string? databasePath = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["CentralServerUrl"] = centralServerUrl,
                // The long-polling handshake can't go through fetch().
                ["Signaling:Transport"] = "WebSocket",
                ["UseSqlite"] = databasePath == null ? "false" : "true",
                ["ConnectionStrings:DBData"] = databasePath == null ? "nordvik-wasm-host" : $"Data Source={databasePath}",
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
                settings[key] = value;
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        /// <param name="files">
        /// Where uploads over the database's size and files cards save as files are kept
        /// (<see cref="BrowserFileStorage"/> on OPFS in the browser host); without it, the
        /// runtime's filesystem (in a browser: its memory, gone on a reload).
        /// </param>
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IBrowserRtc rtc, IFileStorageProvider? files = null)
        {
            services.AddSingleton(configuration);
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                // AddConsole throws in a browser (it probes the terminal).
                logging.AddProvider(new BrowserConsoleLoggerProvider(LogLevel.Information));
                logging.SetMinimumLevel(LogLevel.Information);
            });

            // SQLite (UseSqlite: Infrastructure's own), or else an in-memory database named by
            // ConnectionStrings:DBData.
            InfrastructureLayerModule.Register(services, configuration,
                (options, name) => options.UseInMemoryDatabase(name ?? "nordvik-wasm-host"));
            // No settings file in a browser: fixed settings, constant upload limits.
            services.Replace(ServiceDescriptor.Singleton<NordvikManager.Infrastructure.Interfaces.IServerSettingsStore, FixedServerSettingsStore>());
            if (files != null)
                services.Replace(ServiceDescriptor.Singleton(files));
            ApplicationLayerModule.Register(services, configuration);
            RealTimeLayerModule.Register(services);

            services.AddSingleton<HeldCentralTokens>();
            services.AddSingleton<ICentralTokenStore>(sp => sp.GetRequiredService<HeldCentralTokens>());
            services.AddSingleton<ICentralServerService, CentralServerService>();
            services.AddSingleton<IPeerConnectionFactory>(new BrowserPeerConnectionFactory(rtc));
            // Tunnelled REST from the shared endpoints; routes not moved to them yet answer 501.
            services.AddSingleton(ApiRoutes.Shared);
            services.AddSingleton<ApiRouter>();
            services.AddSingleton<IWebRTCApiDispatcher>(sp => sp.GetRequiredService<ApiRouter>());
            // GET api/Host/Info: a browser host, and what it doesn't provide.
            services.AddSingleton<IHostDescription, BrowserHostDescription>();

            // The GM using this host in its own tab, and so its local admin.
            services.AddSingleton<HostGm>();
            services.AddSingleton<ILocalAdminService, HostLocalAdmin>();
        }

        /// <summary>Every registration is checked as the provider is built.</summary>
        /// <param name="configure">Changes after the host's own registrations (e.g. another <see cref="ISignalingService"/>).</param>
        public static ServiceProvider Build(IConfiguration configuration, IBrowserRtc rtc, Action<IServiceCollection>? configure = null, IFileStorageProvider? files = null)
        {
            var services = new ServiceCollection();
            ConfigureServices(services, configuration, rtc, files);
            configure?.Invoke(services);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            ApplicationLayerModule.AfterBuild(provider);
            if (configuration.GetValue<bool>("UseSqlite"))
                MigrateSqlite(provider, configuration);
            return provider;
        }

        // As the native server's startup: the real migrations, with a rollback journal throughout.
        // EF Core turns WAL on when it creates a database, and WAL needs files the browser host's
        // OPFS filesystem doesn't have (-wal, and an mmap-ed -shm): so a new file is made here
        // first, with a rollback journal, and EF finds a database already there.
        private static void MigrateSqlite(IServiceProvider provider, IConfiguration configuration)
        {
            // SQLite doesn't make the file's folder (a browser's filesystem starts empty).
            var file = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(configuration.GetConnectionString("DBData")).DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(file) || new FileInfo(file).Length == 0)
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(configuration.GetConnectionString("DBData"));
                connection.Open();
                using var create = connection.CreateCommand();
                // A write, so the file has its header and EF sees a database.
                create.CommandText = "PRAGMA journal_mode=DELETE; CREATE TABLE __nordvik_new (x); DROP TABLE __nordvik_new;";
                create.ExecuteNonQuery();
            }

            using var scope = provider.CreateScope();
            var database = ((DbContext)scope.ServiceProvider.GetRequiredService<NordvikManager.Infrastructure.Interfaces.IDbContext>()).Database;
            database.Migrate();
            database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
        }
    }
}
