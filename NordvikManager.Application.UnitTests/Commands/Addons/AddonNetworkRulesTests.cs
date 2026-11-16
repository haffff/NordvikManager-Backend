using NordvikManager.Application.Commands.Addons;
using NordvikManager.Domain.Entities;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // An addon's info.json "network": where its code connects outside NordvikManager. The
    // server keeps only real origins from it, and holds the addon's actions to it.
    public class AddonNetworkRulesTests
    {
        [Fact]
        public void Normalize_KeepsOriginsOnly_LowerCase_NoDuplicates()
        {
            var network = AddonNetworkRules.Normalize(new AddonNetwork
            {
                Hosts = new() { "https://API.example.com", "https://api.example.com", "wss://live.example.com:8443", "https://x.example.com/path", "ftp://f.example.com", "nope", "https://*.example.com", "" },
                UserHosts = "  Your Trilium server  ",
            });

            Assert.Equal(new[] { "https://api.example.com", "wss://live.example.com:8443" }, network!.Hosts);
            Assert.Equal("Your Trilium server", network.UserHosts);
        }

        [Fact]
        public void Normalize_Undeclared_StaysNull_AndAnEmptyDeclarationStaysEmpty()
        {
            Assert.Null(AddonNetworkRules.Normalize(null));

            var none = AddonNetworkRules.Normalize(new AddonNetwork { UserHosts = "   " });
            Assert.NotNull(none);
            Assert.Empty(none!.Hosts!);
            Assert.Null(none.UserHosts);
        }

        [Fact]
        public void Normalize_CapsLongText()
        {
            var network = AddonNetworkRules.Normalize(new AddonNetwork { UserHosts = new string('x', 500) });

            Assert.Equal(200, network!.UserHosts!.Length);
        }

        [Theory]
        [InlineData("https://api.example.com/v1/roll?x=1", true)]
        [InlineData("https://API.example.com/v1", true)]
        [InlineData("https://api.example.com:443/v1", true)] // the default port is the same origin
        [InlineData("https://other.example.com/x", false)]
        [InlineData("http://api.example.com/x", false)] // another scheme is another origin
        [InlineData("not a url", false)]
        public void Allows_OnlyDeclaredOrigins(string url, bool allowed)
        {
            var network = new AddonNetwork { Hosts = new() { "https://api.example.com" } };

            Assert.Equal(allowed, AddonNetworkRules.Allows(network, url));
        }

        [Fact]
        public void Allows_AnyAddress_WhenTheUserPicksIt_OrTheAddonDoesntSay()
        {
            Assert.True(AddonNetworkRules.Allows(new AddonNetwork { UserHosts = "Your server" }, "https://anything.example.com/x"));
            Assert.True(AddonNetworkRules.Allows(null, "https://anything.example.com/x"));
            Assert.False(AddonNetworkRules.Allows(new AddonNetwork(), "https://anything.example.com/x"));
        }
    }
}
