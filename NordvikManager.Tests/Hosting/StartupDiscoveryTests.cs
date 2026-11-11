using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.WebSockets.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NordvikManager.Tests.Hosting
{
    // WebSocket handlers and action steps are found by reflection, in the assembly that holds
    // them (NordvikManager.RealTime, shared with the browser-wasm host). Scanning the wrong
    // assembly registers none, which builds and starts fine and only shows in a game.
    public class StartupDiscoveryTests
    {
        private static IServiceCollection ConfiguredServices()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:DBData"] = "Data Source=:memory:",
            }).Build();
            var services = new ServiceCollection();
            new Startup(configuration).ConfigureServices(services);
            return services;
        }

        private static Type[] Concrete<T>() => typeof(T).Assembly.GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToArray();

        private static HashSet<Type> Registered<T>(IServiceCollection services) => services
            .Where(d => d.ServiceType == typeof(T))
            .Select(d => d.ImplementationType!)
            .ToHashSet();

        [Fact]
        public void ConfigureServices_RegistersEveryWebSocketHandler()
        {
            var expected = Concrete<IWebSocketHandler>();

            Assert.NotEmpty(expected);
            Assert.Subset(Registered<IWebSocketHandler>(ConfiguredServices()), expected.ToHashSet());
        }

        [Fact]
        public void ConfigureServices_RegistersEveryActionStepDefinition()
        {
            var expected = Concrete<IActionStepDefinition>();

            Assert.NotEmpty(expected);
            Assert.Subset(Registered<IActionStepDefinition>(ConfiguredServices()), expected.ToHashSet());
        }

        // Thumbnails come from the host: SkiaSharp natively, so Application doesn't carry it.
        [Fact]
        public void ConfigureServices_RegistersTheSkiaThumbnailGenerator()
        {
            Assert.Equal(new[] { typeof(NordvikManager.Imaging.SkiaThumbnailGenerator) },
                ConfiguredServices().Where(d => d.ServiceType == typeof(NordvikManager.Application.Helpers.IThumbnailGenerator)).Select(d => d.ImplementationType));
        }
    }
}
