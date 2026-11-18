using Microsoft.Extensions.Configuration;
using NordvikManager.Infrastructure.Services;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Application.UnitTests.Infrastructure.Addons
{
    // Without an addon registry, only what needs the registry fails. InstallAddonCommandHandler
    // takes this service for every install, so a constructor that threw made installing a
    // built-in addon (Basics, no registry involved) fail too: always in the browser host, which
    // sets no registry, and natively once a GM cleared it in the server settings.
    public class AddonRepositoryWithoutRegistryTests
    {
        private static AddonRegistryOptions NoRegistry() => AddonRegistryOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

        [Fact]
        public void Constructor_Succeeds_WithoutARegistry()
        {
            var service = new AddonRepositoryService(NoRegistry());

            Assert.NotNull(service);
        }

        [Fact]
        public async Task GetRepository_SaysNoRegistryIsSet_WithoutOne()
        {
            var service = new AddonRepositoryService(NoRegistry());

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRepository());
            Assert.Contains("No addon registry is set", thrown.Message);
        }

        [Fact]
        public async Task GetAddonByKey_SaysNoRegistryIsSet_WithoutOne()
        {
            var service = new AddonRepositoryService(NoRegistry());

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAddonByKey("dnd5e"));
            Assert.Contains("No addon registry is set", thrown.Message);
        }
    }
}
