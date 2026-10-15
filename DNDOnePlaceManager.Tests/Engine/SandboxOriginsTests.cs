using DNDOnePlaceManager.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using Xunit;

namespace DNDOnePlaceManager.Tests.Engine
{
    // Security:SandboxAllowedOrigins is the GM's list of hosts that addon cards and Html
    // chat messages may load from. The backend enforces it on its own sandbox page and
    // hands it to clients, which enforce it on the player side.
    public class SandboxOriginsTests
    {
        public static SandboxOrigins Create(params string[] allowed)
        {
            var settings = new Dictionary<string, string?>();
            for (var i = 0; i < allowed.Length; i++)
                settings[$"Security:SandboxAllowedOrigins:{i}"] = allowed[i];
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new SandboxOrigins(config, NullLogger<SandboxOrigins>.Instance);
        }

        [Fact]
        public void WithoutConfiguration_UsesTheDefaultHosts()
        {
            Assert.Equal(new[]
            {
                "https://raw.githubusercontent.com",
                "https://api.github.com",
                "https://fonts.googleapis.com",
                "https://fonts.gstatic.com",
            }, Create().Origins);
        }

        [Fact]
        public void UsesTheConfiguredHosts()
        {
            Assert.Equal(new[] { "https://cdn.example.com", "https://*.example.org:8443" },
                Create("https://cdn.example.com", " https://*.example.org:8443 ").Origins);
        }

        [Theory]
        [InlineData("'unsafe-eval'")]
        [InlineData("https://a.com; script-src *")]
        [InlineData("https://a.com\" onload=\"x")]
        [InlineData("http://plain.example.com")]
        [InlineData("*")]
        [InlineData("https://a.com/path")]
        public void DropsEntriesThatAreNotHttpsOrigins(string bad)
        {
            Assert.Equal(new[] { "https://ok.example.com" }, Create("https://ok.example.com", bad).Origins);
        }
    }
}
