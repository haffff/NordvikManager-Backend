using DndOnePlaceManager.Infrastructure.Services.Settings;
using Microsoft.Extensions.Configuration;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Settings
{
    // The addon registry (where addons are listed and downloaded from) and the featured
    // addons offered at game creation: appsettings AddonsConfiguration, unless the local
    // admin saved their own in Application Settings. A saved value replaces the config one.
    public class AddonRegistryOptionsTests
    {
        private static AddonRegistryOptions FromConfig(string? repository, params string[] featured)
        {
            var values = new Dictionary<string, string?>();
            if (repository != null)
                values["AddonsConfiguration:MainRepository"] = repository;
            for (var i = 0; i < featured.Length; i++)
                values[$"AddonsConfiguration:FeaturedAddons:{i}"] = featured[i];
            return AddonRegistryOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }

        [Fact]
        public void FromConfiguration_ReadsTheRegistryAndFeatured()
        {
            var options = FromConfig("https://repo.example/addons.json", "dnd5e", "roll20compat");

            Assert.Equal("https://repo.example/addons.json", options.MainRepository);
            Assert.Equal(new[] { "dnd5e", "roll20compat" }, options.FeaturedAddons);
            Assert.Equal("https://repo.example/addons.json", options.ConfiguredMainRepository);
        }

        [Fact]
        public void NothingConfigured_HasNoRegistry()
        {
            var options = FromConfig(null);

            Assert.Null(options.MainRepository);
            Assert.Empty(options.FeaturedAddons);
        }

        [Fact]
        public void SavedValues_ReplaceTheConfiguredOnes()
        {
            var options = FromConfig("https://repo.example/addons.json", "dnd5e");

            options.SetRuntime(new AddonSettings { MainRepository = "https://mine.example/addons.json", FeaturedAddons = new List<string> { "themes" } });

            Assert.Equal("https://mine.example/addons.json", options.MainRepository);
            Assert.Equal(new[] { "themes" }, options.FeaturedAddons);
            Assert.Equal(new[] { "dnd5e" }, options.ConfiguredFeaturedAddons);
        }

        [Fact]
        public void NothingSaved_FallsBackToTheConfiguredOnes()
        {
            var options = FromConfig("https://repo.example/addons.json", "dnd5e");
            options.SetRuntime(new AddonSettings { MainRepository = "https://mine.example/addons.json", FeaturedAddons = new List<string>() });

            options.SetRuntime(new AddonSettings());

            Assert.Equal("https://repo.example/addons.json", options.MainRepository);
            Assert.Equal(new[] { "dnd5e" }, options.FeaturedAddons);
        }

        [Fact]
        public void AnEmptySavedFeaturedList_FeaturesNothing()
        {
            var options = FromConfig("https://repo.example/addons.json", "dnd5e");

            options.SetRuntime(new AddonSettings { FeaturedAddons = new List<string>() });

            Assert.Empty(options.FeaturedAddons);
        }

        [Theory]
        [InlineData("https://raw.githubusercontent.com/haffff/NordvikManager-Addons/refs/heads/main/addons.json", true)]
        [InlineData("https://repo.example:8443/addons.json", true)]
        [InlineData("http://repo.example/addons.json", false)]
        [InlineData("/addons.json", false)]
        [InlineData("file:///etc/passwd", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("https://", false)]
        [InlineData("https://user:pass@repo.example/addons.json", false)]
        public void IsValidRepositoryUrl_AcceptsOnlyAbsoluteHttps(string url, bool valid)
        {
            Assert.Equal(valid, AddonRegistryOptions.IsValidRepositoryUrl(url));
        }

        [Theory]
        [InlineData("dnd5e", true)]
        [InlineData("roll20compat", true)]
        [InlineData("my_addon.v2-beta", true)]
        [InlineData("two words", false)]
        [InlineData("", false)]
        [InlineData("../x", false)]
        public void IsValidAddonKey_AcceptsRegistryStyleKeys(string key, bool valid)
        {
            Assert.Equal(valid, AddonRegistryOptions.IsValidAddonKey(key));
        }
    }
}
