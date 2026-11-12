using Newtonsoft.Json.Linq;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Services.Interfaces;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser host's addon registry is its Central (GET api/addons, readmes, and packages it
    /// downloads by key for a logged-in user): the host build's page may only connect to Central's
    /// origin, and GitHub's release downloads don't allow other origins anyway. Central serves the
    /// same registry a GM server reads from GitHub.
    /// </summary>
    public sealed class CentralAddonRepository : IAddonRepositoryService
    {
        private readonly ICentralServerService central;
        private readonly HeldCentralTokens tokens;

        public CentralAddonRepository(ICentralServerService central, HeldCentralTokens tokens)
        {
            this.central = central;
            this.tokens = tokens;
        }

        /// <summary>addons.json's shape ({ categories, repository }), as the registry's readers expect.</summary>
        public async Task<string> GetRepository()
        {
            var json = await central.GetAddonRegistryAsync()
                ?? throw new InvalidOperationException("Central's addon registry couldn't be read.");
            var registry = JObject.Parse(json);
            return new JObject
            {
                ["categories"] = registry["categories"] ?? new JArray(),
                ["repository"] = registry["addons"] ?? new JArray(),
            }.ToString();
        }

        public Task<byte[]> GetAddonByKey(string key) => central.GetAddonPackageAsync(tokens.AccessToken ?? "", key);

        public Task<AddonReadmeResult?> GetReadmeByKey(string key) => central.GetAddonReadmeAsync(key);
    }
}
