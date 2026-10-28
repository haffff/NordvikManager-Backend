using Microsoft.Extensions.Configuration;
using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    public class BrowserLauncherTests
    {
        private static IConfiguration Config(string? openBrowser = null) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(openBrowser == null
                    ? new Dictionary<string, string?>()
                    : new Dictionary<string, string?> { ["OpenBrowser"] = openBrowser })
                .Build();

        [Fact]
        public void ShouldOpen_True_ForAnInteractiveReleaseStart()
        {
            Assert.True(BrowserLauncher.ShouldOpen(noBrowserFlag: false, Config(), isDevelopment: false, interactive: true));
        }

        [Fact]
        public void ShouldOpen_False_WithNoBrowserFlag()
        {
            Assert.False(BrowserLauncher.ShouldOpen(noBrowserFlag: true, Config(), isDevelopment: false, interactive: true));
        }

        [Fact]
        public void ShouldOpen_False_WhenTurnedOffInSettings()
        {
            Assert.False(BrowserLauncher.ShouldOpen(noBrowserFlag: false, Config("false"), isDevelopment: false, interactive: true));
        }

        [Fact]
        public void ShouldOpen_False_WithoutAConsoleUser()
        {
            // Services, containers and redirected output (a hosted install) have nobody to show a browser to.
            Assert.False(BrowserLauncher.ShouldOpen(noBrowserFlag: false, Config(), isDevelopment: false, interactive: false));
        }

        [Fact]
        public void ShouldOpen_False_InDevelopment()
        {
            // launchSettings already opens one, and the frontend usually runs from its own dev server.
            Assert.False(BrowserLauncher.ShouldOpen(noBrowserFlag: false, Config(), isDevelopment: true, interactive: true));
        }

        [Fact]
        public void SplitArgs_RemovesNoBrowserFlag_AndReportsIt()
        {
            var (remaining, noBrowser) = BrowserLauncher.SplitArgs(new[] { "--urls=http://localhost:9000", "--no-browser" });

            Assert.True(noBrowser);
            Assert.Equal(new[] { "--urls=http://localhost:9000" }, remaining);
        }

        [Fact]
        public void SplitArgs_KeepsArgsUntouched_WithoutTheFlag()
        {
            var (remaining, noBrowser) = BrowserLauncher.SplitArgs(new[] { "--urls=http://localhost:9000" });

            Assert.False(noBrowser);
            Assert.Equal(new[] { "--urls=http://localhost:9000" }, remaining);
        }
    }
}
