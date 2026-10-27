using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    // A GM double-clicking the exe saw a wall of framework log lines and no hint what to do next.
    public class StartupBannerTests
    {
        private static StartupInfo Info(bool? centralReachable = true) => new(
            Version: "0.2.0",
            Urls: new[] { "http://localhost:8213", "https://localhost:8214" },
            CentralServerUrl: "https://nordvikmanager.pl",
            CentralReachable: centralReachable,
            DataFolder: @"C:\Games\NordvikManager\data",
            LogFolder: @"C:\Games\NordvikManager\data\logs");

        [Fact]
        public void Format_ShowsVersionAddressesAndFolders()
        {
            var text = StartupBanner.Format(Info());

            Assert.Contains("NordvikManager", text);
            Assert.Contains("ver 0.2.0", text);
            Assert.Contains("Server is running", text);
            Assert.Contains("http://localhost:8213", text);
            Assert.Contains("https://localhost:8214", text);
            Assert.Contains(@"C:\Games\NordvikManager\data", text);
            Assert.Contains(@"C:\Games\NordvikManager\data\logs", text);
            Assert.Contains("Keep this window open", text);
            Assert.Contains("Ctrl+C", text);
        }

        [Fact]
        public void Format_StartsWithTheMoonLogo_NameBesideIt()
        {
            var lines = StartupBanner.Format(Info()).Split(Environment.NewLine);

            var logo = lines.First(l => l.Contains("_..._"));
            var name = lines.First(l => l.Contains("NordvikManager"));
            Assert.True(Array.IndexOf(lines, logo) < Array.IndexOf(lines, name));
            Assert.Contains(".'  .-'", name);
        }

        [Fact]
        public void Format_SaysCentralIsConnected_WhenReachable()
        {
            var text = StartupBanner.Format(Info(centralReachable: true));

            Assert.Contains("https://nordvikmanager.pl (connected)", text);
        }

        [Fact]
        public void Format_WarnsInPlainWords_WhenCentralIsUnreachable()
        {
            var text = StartupBanner.Format(Info(centralReachable: false));

            Assert.Contains("NOT reachable", text);
            Assert.Contains("internet connection", text);
        }

        [Fact]
        public void Format_StaysAscii_SoEveryConsoleShowsIt()
        {
            var text = StartupBanner.Format(Info(centralReachable: false));

            Assert.All(text, c => Assert.True(c < 128, $"non-ASCII character '{c}'"));
        }

        [Theory]
        [InlineData("http://[::]:8213", "http://localhost:8213")]
        [InlineData("http://0.0.0.0:8213", "http://localhost:8213")]
        [InlineData("http://+:8213", "http://localhost:8213")]
        [InlineData("http://*:8213", "http://localhost:8213")]
        [InlineData("http://127.0.0.1:8213", "http://localhost:8213")]
        [InlineData("http://localhost:8213", "http://localhost:8213")]
        [InlineData("https://192.168.1.20:8214", "https://192.168.1.20:8214")]
        public void ForBrowser_TurnsBindAddressesIntoOpenableUrls(string bound, string expected)
        {
            Assert.Equal(new[] { expected }, ServerAddresses.ForBrowser(new[] { bound }));
        }

        [Fact]
        public void ForBrowser_DropsDuplicates()
        {
            // Kestrel reports localhost once per IP family.
            var urls = ServerAddresses.ForBrowser(new[] { "http://127.0.0.1:8213", "http://[::1]:8213" });

            Assert.Equal(new[] { "http://localhost:8213" }, urls);
        }

        [Fact]
        public void BrowserUrl_PrefersHttp()
        {
            // Without a trusted certificate the https address shows a browser warning first.
            Assert.Equal("http://localhost:8213",
                ServerAddresses.BrowserUrl(new[] { "https://localhost:8214", "http://localhost:8213" }));
        }

        [Fact]
        public void BrowserUrl_IsNull_WithoutAddresses()
        {
            Assert.Null(ServerAddresses.BrowserUrl(Array.Empty<string>()));
        }
    }
}
