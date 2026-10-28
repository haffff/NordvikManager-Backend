using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Infrastructure;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Application.Commands.ServerSettings;
using NordvikManager.Application.DataTransferObjects.ServerSettings;
using NordvikManager.Infrastructure.Services.Proxy;
using NordvikManager.Infrastructure.Services.Sandbox;
using NordvikManager.Infrastructure.Services.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace NordvikManager.Application.UnitTests.Infrastructure.Proxy
{
    // MediatR builds handlers only when a command is sent, so a missing registration would
    // first show when an action runs SendRequest or DownloadResource. Resolve them here.
    public class ProxyWiringTests
    {
        private static ServiceProvider Container(params (string Key, string Value)[] settings)
        {
            var values = new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:DBData"] = "Data Source=:memory:",
            };
            foreach (var (key, value) in settings)
                values[key] = value;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            InfrastructureLayerModule.Register(services, configuration);
            ApplicationLayerModule.Register(services, configuration);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Register_ResolvesTheProxyAndDownloadHandlers()
        {
            using var provider = Container();
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<ProxyCommand, ProxyCommandResult>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<DownloadResourceCommand, DownloadResourceResult>>());
            Assert.IsType<ProxyHttpService>(scope.ServiceProvider.GetRequiredService<IProxyHttpService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<GetServerSettingsCommand, ServerSettingsDTO>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<SetSandboxAllowedOriginsCommand, ServerSettingsDTO>>());
        }

        [Fact]
        public void Register_AppliesSavedSandboxOrigins_AtStartAndOnEverySave()
        {
            var folder = Path.Combine(Path.GetTempPath(), "nordvik-wiring-" + Guid.NewGuid().ToString("N"));
            var file = Path.Combine(folder, "server-settings.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, """{ "Security": { "SandboxAllowedOrigins": [ "https://cdn.example.com" ] } }""");
            try
            {
                using var provider = Container(("ServerSettings:FilePath", file));
                var origins = provider.GetRequiredService<SandboxOrigins>();
                Assert.Contains("https://cdn.example.com", origins.Origins);
                Assert.Contains("https://fonts.gstatic.com", origins.Origins);

                provider.GetRequiredService<IServerSettingsStore>().Update(s => s.Security.SandboxAllowedOrigins = new List<string> { "https://new.example.com" });

                Assert.DoesNotContain("https://cdn.example.com", origins.Origins);
                Assert.Contains("https://new.example.com", origins.Origins);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void Register_AppliesTheSavedAddonRegistry_AtStartAndOnEverySave()
        {
            var folder = Path.Combine(Path.GetTempPath(), "nordvik-wiring-" + Guid.NewGuid().ToString("N"));
            var file = Path.Combine(folder, "server-settings.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, """{ "Addons": { "MainRepository": "https://mine.example/addons.json" } }""");
            try
            {
                using var provider = Container(("ServerSettings:FilePath", file), ("AddonsConfiguration:MainRepository", "https://repo.example/addons.json"), ("AddonsConfiguration:FeaturedAddons:0", "dnd5e"));
                var registry = provider.GetRequiredService<AddonRegistryOptions>();
                Assert.Equal("https://mine.example/addons.json", registry.MainRepository);
                Assert.Equal(new[] { "dnd5e" }, registry.FeaturedAddons);

                provider.GetRequiredService<IServerSettingsStore>().Update(s => s.Addons.MainRepository = null);

                Assert.Equal("https://repo.example/addons.json", registry.MainRepository);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void Register_ReadsTheAllowedPrivateHosts()
        {
            using var provider = Container(("Security:ProxyAllowedPrivateHosts:0", "localhost:37840"), ("Security:ProxyAllowedPrivateHosts:1", "not a host"));

            var options = provider.GetRequiredService<ProxyOptions>();

            Assert.True(ProxyAddressRules.IsAllowed("localhost", 37840, new[] { IPAddress.Loopback }, options));
            Assert.Contains("not a host", options.Ignored);
        }

        [Fact]
        public void Register_AppliesTheSettingsFile_AtStartAndOnEverySave()
        {
            var folder = Path.Combine(Path.GetTempPath(), "nordvik-wiring-" + Guid.NewGuid().ToString("N"));
            var file = Path.Combine(folder, "server-settings.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, """{ "Security": { "ProxyAllowedPrivateHosts": [ "localhost:8080" ] } }""");
            try
            {
                using var provider = Container(("ServerSettings:FilePath", file));
                var options = provider.GetRequiredService<ProxyOptions>();
                Assert.True(ProxyAddressRules.IsAllowed("localhost", 8080, new[] { IPAddress.Loopback }, options));

                provider.GetRequiredService<IServerSettingsStore>().Update(s => s.Security.ProxyAllowedPrivateHosts = new List<string> { "localhost:9090" });

                Assert.False(ProxyAddressRules.IsAllowed("localhost", 8080, new[] { IPAddress.Loopback }, options));
                Assert.True(ProxyAddressRules.IsAllowed("localhost", 9090, new[] { IPAddress.Loopback }, options));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
