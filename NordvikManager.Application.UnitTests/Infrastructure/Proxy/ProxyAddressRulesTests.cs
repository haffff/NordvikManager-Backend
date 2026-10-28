using NordvikManager.Infrastructure.Services.Proxy;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace NordvikManager.Application.UnitTests.Infrastructure.Proxy
{
    // SendRequest and DownloadResource may reach public hosts only, unless the server's owner
    // lists a private one (e.g. a Trilium on localhost) in Security:ProxyAllowedPrivateHosts.
    public class ProxyAddressRulesTests
    {
        private static readonly ProxyOptions None = ProxyOptions.From(Array.Empty<string>());

        private static IPAddress[] Ips(params string[] ips) => ips.Select(IPAddress.Parse).ToArray();

        [Fact]
        public void IsAllowed_PublicAddress_IsAllowed()
        {
            Assert.True(ProxyAddressRules.IsAllowed("example.com", 443, Ips("8.8.8.8"), None));
            Assert.True(ProxyAddressRules.IsAllowed("example.com", 443, Ips("172.32.0.1"), None));
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.1.2.3")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.1.1")]
        [InlineData("169.254.1.1")]
        [InlineData("0.0.0.0")]
        [InlineData("::1")]
        [InlineData("fc00::1")]
        [InlineData("fe80::1")]
        [InlineData("::ffff:10.0.0.1")]
        [InlineData("::ffff:127.0.0.1")]
        public void IsAllowed_PrivateAddress_IsBlocked(string ip)
        {
            Assert.False(ProxyAddressRules.IsAllowed("somewhere.example", 80, Ips(ip), None));
        }

        [Fact]
        public void IsAllowed_AnyPrivateAddressAmongSeveral_IsBlocked()
        {
            Assert.False(ProxyAddressRules.IsAllowed("example.com", 80, Ips("8.8.8.8", "10.0.0.1"), None));
        }

        [Fact]
        public void IsAllowed_NoAddress_IsBlocked()
        {
            Assert.False(ProxyAddressRules.IsAllowed("example.com", 80, Ips(), None));
        }

        [Fact]
        public void IsAllowed_ListedHostWithPort_IsAllowedOnThatPortOnly()
        {
            var options = ProxyOptions.From(new[] { "192.168.1.20:8080" });

            Assert.True(ProxyAddressRules.IsAllowed("192.168.1.20", 8080, Ips("192.168.1.20"), options));
            Assert.False(ProxyAddressRules.IsAllowed("192.168.1.20", 8081, Ips("192.168.1.20"), options));
        }

        [Fact]
        public void IsAllowed_ListedHostWithoutPort_IsAllowedOnAnyPort_IgnoringCase()
        {
            var options = ProxyOptions.From(new[] { "localhost" });

            Assert.True(ProxyAddressRules.IsAllowed("LocalHost", 37840, Ips("127.0.0.1", "::1"), options));
        }

        // The list names hosts, not addresses: another name for the same private address isn't allowed.
        [Fact]
        public void IsAllowed_ListedName_DoesNotAllowOtherNamesForTheSameAddress()
        {
            var options = ProxyOptions.From(new[] { "trilium.lan" });

            Assert.True(ProxyAddressRules.IsAllowed("trilium.lan", 8080, Ips("192.168.1.20"), options));
            Assert.False(ProxyAddressRules.IsAllowed("router.lan", 80, Ips("192.168.1.20"), options));
        }

        [Fact]
        public void IsAllowed_ListedIpv6InBrackets_MatchesTheBareAddress()
        {
            var options = ProxyOptions.From(new[] { "[::1]:8080" });

            Assert.True(ProxyAddressRules.IsAllowed("::1", 8080, Ips("::1"), options));
            Assert.True(ProxyAddressRules.IsAllowed("[::1]", 8080, Ips("::1"), options));
        }

        [Theory]
        [InlineData("http://localhost:8080")]
        [InlineData("localhost/etapi")]
        [InlineData("*.lan")]
        [InlineData("two words")]
        [InlineData("localhost:99999")]
        [InlineData("")]
        public void From_InvalidEntry_IsIgnoredAndReported(string entry)
        {
            var options = ProxyOptions.From(new[] { entry, "localhost:8080" });

            Assert.Contains(entry, options.Ignored);
            Assert.Single(options.AllowedPrivateHosts);
        }

        [Fact]
        public void FromConfiguration_ReadsTheListUnderSecurity()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ProxyAllowedPrivateHosts:0"] = "localhost:37840",
                ["Security:ProxyAllowedPrivateHosts:1"] = "trilium.lan",
            }).Build();

            var options = ProxyOptions.FromConfiguration(configuration);

            Assert.True(ProxyAddressRules.IsAllowed("localhost", 37840, Ips("127.0.0.1"), options));
            Assert.True(ProxyAddressRules.IsAllowed("trilium.lan", 80, Ips("10.0.0.5"), options));
        }

        [Fact]
        public void FromConfiguration_NothingSet_AllowsNoPrivateHost()
        {
            var options = ProxyOptions.FromConfiguration(new ConfigurationBuilder().Build());

            Assert.Empty(options.AllowedPrivateHosts);
            Assert.False(ProxyAddressRules.IsAllowed("localhost", 80, Ips("127.0.0.1"), options));
        }

        // Hosts the local admin adds in Application Settings (server-settings.json) sit on top
        // of the appsettings ones and replace each other on every save.

        [Fact]
        public void SetRuntimeHosts_AddsToTheConfiguredOnes()
        {
            var options = ProxyOptions.From(new[] { "trilium.lan" });

            options.SetRuntimeHosts(new[] { "localhost:8080" });

            Assert.True(ProxyAddressRules.IsAllowed("localhost", 8080, Ips("127.0.0.1"), options));
            Assert.True(ProxyAddressRules.IsAllowed("trilium.lan", 80, Ips("10.0.0.5"), options));
            Assert.Equal(new[] { "trilium.lan" }, options.ConfiguredEntries);
            Assert.Equal(new[] { "localhost:8080" }, options.RuntimeEntries);
        }

        [Fact]
        public void SetRuntimeHosts_ReplacesThePreviousList_ButNeverTheConfiguredOne()
        {
            var options = ProxyOptions.From(new[] { "trilium.lan" });
            options.SetRuntimeHosts(new[] { "localhost:8080" });

            options.SetRuntimeHosts(Array.Empty<string>());

            Assert.False(ProxyAddressRules.IsAllowed("localhost", 8080, Ips("127.0.0.1"), options));
            Assert.True(ProxyAddressRules.IsAllowed("trilium.lan", 80, Ips("10.0.0.5"), options));
        }

        [Fact]
        public void SetRuntimeHosts_ReturnsAndSkipsInvalidEntries()
        {
            var options = ProxyOptions.From(Array.Empty<string>());

            var ignored = options.SetRuntimeHosts(new[] { "http://localhost", "localhost:8080" });

            Assert.Equal(new[] { "http://localhost" }, ignored);
            Assert.Equal(new[] { "localhost:8080" }, options.RuntimeEntries);
        }
    }
}
