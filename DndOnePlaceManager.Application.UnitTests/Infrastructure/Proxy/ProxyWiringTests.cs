using DndOnePlaceManager.Application.Commands.Properties.Proxy;
using DndOnePlaceManager.Infrastructure;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
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
    }
}
