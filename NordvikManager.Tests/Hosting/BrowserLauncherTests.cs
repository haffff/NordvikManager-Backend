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

        // Only an interactive release start opens the browser. Not with --no-browser, not when
        // OpenBrowser is off in the settings, not without a console user (services, containers
        // and redirected output in a hosted install have nobody to show a browser to), and not
        // in development (launchSettings already opens one, and the frontend usually runs from
        // its own dev server).
        [Theory]
        [InlineData(false, null, false, true, true)]
        [InlineData(true, null, false, true, false)]
        [InlineData(false, "false", false, true, false)]
        [InlineData(false, null, false, false, false)]
        [InlineData(false, null, true, true, false)]
        public void ShouldOpen(bool noBrowserFlag, string? openBrowserSetting, bool isDevelopment, bool interactive, bool opens)
        {
            Assert.Equal(opens, BrowserLauncher.ShouldOpen(noBrowserFlag, Config(openBrowserSetting), isDevelopment, interactive));
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
