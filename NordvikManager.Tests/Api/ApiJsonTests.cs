using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NordvikManager.Api;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace NordvikManager.Tests.Api
{
    // The player client reads the same JSON whichever host answers: the controllers' (ASP.NET
    // Core with Startup's options) and the shared router's (ApiJson) must not drift apart.
    public class ApiJsonTests
    {
        private sealed class Sample
        {
            public Guid GameId { get; set; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
            public string? Missing { get; set; }
            public int Count { get; set; } = 3;
            public List<string> Names { get; set; } = new() { "Alice" };
        }

        private static JsonSerializerOptions ControllersJson()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:DBData"] = "Data Source=:memory:",
            }).Build();
            var services = new ServiceCollection();
            new Startup(configuration).ConfigureServices(services);
            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        }

        [Fact]
        public void TheRoutersJson_IsTheControllers()
        {
            var controllers = ControllersJson();

            Assert.Equal(JsonSerializer.Serialize(new Sample(), controllers), JsonSerializer.Serialize(new Sample(), ApiJson.Options));
            Assert.Equal("{\"gameId\":\"11111111-2222-3333-4444-555555555555\",\"count\":3,\"names\":[\"Alice\"]}",
                JsonSerializer.Serialize(new Sample(), ApiJson.Options));
        }

        [Fact]
        public void TheRouterReadsBodies_AsTheControllersDo()
        {
            var controllers = ControllersJson();
            const string body = "{\"COUNT\":\"7\",\"names\":[\"Bob\",],}";

            Assert.Equal(JsonSerializer.Deserialize<Sample>(body, controllers)!.Count, JsonSerializer.Deserialize<Sample>(body, ApiJson.Options)!.Count);
            Assert.Equal(7, JsonSerializer.Deserialize<Sample>(body, ApiJson.Options)!.Count);
        }
    }
}
