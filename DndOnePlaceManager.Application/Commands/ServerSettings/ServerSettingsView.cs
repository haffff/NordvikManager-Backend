using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Sandbox;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    /// <summary>What the settings handlers share: the saved file, whether it may change, and the appsettings side of each setting.</summary>
    public class ServerSettingsView
    {
        public const int MaxListEntries = 50;

        public IServerSettingsStore Store { get; }
        private readonly ServerSettingsOptions options;
        private readonly ProxyOptions proxyOptions;
        private readonly SandboxOrigins sandboxOrigins;
        private readonly AddonRegistryOptions addonRegistry;

        public ServerSettingsView(IServerSettingsStore store, ServerSettingsOptions options, ProxyOptions proxyOptions, SandboxOrigins sandboxOrigins, AddonRegistryOptions addonRegistry)
        {
            Store = store;
            this.options = options;
            this.proxyOptions = proxyOptions;
            this.sandboxOrigins = sandboxOrigins;
            this.addonRegistry = addonRegistry;
        }

        public void ThrowIfLocked()
        {
            if (!options.AllowRuntimeChanges)
                throw new ServerSettingsLockedException();
        }

        /// <summary>Trimmed, without blanks or case-insensitive duplicates; throws naming the entries <paramref name="invalid"/> rejects.</summary>
        public static List<string> CleanList(IEnumerable<string?>? entries, Func<IEnumerable<string?>, IReadOnlyList<string>> invalid, string expected)
        {
            var list = (entries ?? Array.Empty<string?>())
                .Select(e => e?.Trim() ?? "")
                .Where(e => e != "")
                .DistinctBy(e => e.ToLowerInvariant())
                .ToList();

            var bad = invalid(list);
            if (bad.Count > 0)
                throw new ServerSettingsException($"Not {expected}: {string.Join(", ", bad)}.");
            if (list.Count > MaxListEntries)
                throw new ServerSettingsException($"At most {MaxListEntries} entries are allowed.");
            return list;
        }

        public ServerSettingsDTO ToDto()
        {
            var saved = Store.Load();
            return new ServerSettingsDTO
            {
                CanEdit = options.AllowRuntimeChanges,
                Error = Store.LoadError,
                ProxyAllowedPrivateHosts = new SettingListDTO
                {
                    Locked = proxyOptions.ConfiguredEntries,
                    Entries = saved.Security.ProxyAllowedPrivateHosts,
                },
                SandboxAllowedOrigins = new SettingListDTO
                {
                    Locked = sandboxOrigins.Configured,
                    Entries = saved.Security.SandboxAllowedOrigins,
                },
                AddonRegistry = new AddonRegistryDTO
                {
                    MainRepository = new SettingValueDTO<string> { FromConfig = addonRegistry.ConfiguredMainRepository, Saved = saved.Addons.MainRepository },
                    FeaturedAddons = new SettingValueDTO<IReadOnlyList<string>> { FromConfig = addonRegistry.ConfiguredFeaturedAddons, Saved = saved.Addons.FeaturedAddons },
                },
            };
        }
    }
}
