using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace NordvikManager.Infrastructure.Services.Settings
{
    /// <summary>
    /// The addon registry addons are listed and downloaded from, and the featured addons offered
    /// at game creation: AddonsConfiguration from appsettings, unless the local admin saved their
    /// own in Application Settings (server-settings.json). A saved value replaces the config one.
    /// </summary>
    public class AddonRegistryOptions
    {
        private static readonly Regex AddonKeyPattern = new(@"^[a-z0-9][a-z0-9_.-]{0,99}\z", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string? ConfiguredMainRepository { get; private init; }

        public IReadOnlyList<string> ConfiguredFeaturedAddons { get; private init; } = Array.Empty<string>();

        // Swapped whole on every save.
        private volatile AddonSettings saved = new();

        public string? MainRepository => string.IsNullOrWhiteSpace(saved.MainRepository) ? ConfiguredMainRepository : saved.MainRepository;

        public IReadOnlyList<string> FeaturedAddons => (IReadOnlyList<string>?)saved.FeaturedAddons ?? ConfiguredFeaturedAddons;

        public static AddonRegistryOptions FromConfiguration(IConfiguration configuration) => new()
        {
            ConfiguredMainRepository = string.IsNullOrWhiteSpace(configuration["AddonsConfiguration:MainRepository"]) ? null : configuration["AddonsConfiguration:MainRepository"],
            ConfiguredFeaturedAddons = configuration.GetSection("AddonsConfiguration:FeaturedAddons").Get<string[]>() ?? Array.Empty<string>(),
        };

        /// <summary>Applies what's saved in server-settings.json (a copy, so later changes to <paramref name="settings"/> don't leak in).</summary>
        public void SetRuntime(AddonSettings settings)
        {
            saved = new AddonSettings
            {
                MainRepository = settings.MainRepository,
                FeaturedAddons = settings.FeaturedAddons?.ToList(),
            };
        }

        /// <summary>An absolute https URL without credentials: over http anyone on the network could swap the addon code.</summary>
        public static bool IsValidRepositoryUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);

        public static bool IsValidAddonKey(string? key) => key != null && AddonKeyPattern.IsMatch(key);
    }
}
