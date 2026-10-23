using DndOnePlaceManager.Application.Commands.Properties.Proxy;
using DndOnePlaceManager.Infrastructure;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Proxy
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
