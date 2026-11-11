using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NordvikManager.Api;
using NordvikManager.Application;
using NordvikManager.Infrastructure;
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
    /// tokens, WebSocket-only signaling and an in-memory database.
    /// </summary>
    public static class WasmHost
    {
        /// <summary>The host's settings; <paramref name="overrides"/> replace any of them.</summary>
        public static IConfiguration Configuration(string centralServerUrl, IDictionary<string, string?>? overrides = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["CentralServerUrl"] = centralServerUrl,
                // The long-polling handshake can't go through fetch().
                ["Signaling:Transport"] = "WebSocket",
                ["UseSqlite"] = "false",
                ["ConnectionStrings:DBData"] = "nordvik-wasm-host",
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
                settings[key] = value;
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IBrowserRtc rtc)
        {
            services.AddSingleton(configuration);
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                // AddConsole throws in a browser (it probes the terminal).
                logging.AddProvider(new BrowserConsoleLoggerProvider(LogLevel.Information));
                logging.SetMinimumLevel(LogLevel.Information);
            });

            // An in-memory database (named by ConnectionStrings:DBData) until browser storage.
            InfrastructureLayerModule.Register(services, configuration,
                (options, name) => options.UseInMemoryDatabase(name ?? "nordvik-wasm-host"));
            // No settings file in a browser: fixed settings, constant upload limits.
            services.Replace(ServiceDescriptor.Singleton<NordvikManager.Infrastructure.Interfaces.IServerSettingsStore, FixedServerSettingsStore>());
            ApplicationLayerModule.Register(services, configuration);
            RealTimeLayerModule.Register(services);

            services.AddSingleton<HeldCentralTokens>();
            services.AddSingleton<ICentralTokenStore>(sp => sp.GetRequiredService<HeldCentralTokens>());
            services.AddSingleton<ICentralServerService, CentralServerService>();
            services.AddSingleton<IPeerConnectionFactory>(new BrowserPeerConnectionFactory(rtc));
            // Tunnelled REST from the shared endpoints; routes not moved to them yet answer 501.
            services.AddSingleton(ApiRoutes.Shared);
            services.AddSingleton<IWebRTCApiDispatcher, ApiRouter>();
        }

        /// <summary>Every registration is checked as the provider is built.</summary>
        /// <param name="configure">Changes after the host's own registrations (e.g. another <see cref="ISignalingService"/>).</param>
        public static ServiceProvider Build(IConfiguration configuration, IBrowserRtc rtc, Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            ConfigureServices(services, configuration, rtc);
            configure?.Invoke(services);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            ApplicationLayerModule.AfterBuild(provider);
            return provider;
        }
    }
}
