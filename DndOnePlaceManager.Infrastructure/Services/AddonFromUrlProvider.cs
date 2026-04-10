using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net;

namespace DndOnePlaceManager.Infrastructure.Services
{
    /// <summary>
    /// Fetches addon metadata and files from the configured MainRepository only.
    /// No external URL is ever accepted from callers — all resolution is internal.
    /// </summary>
    internal class AddonRepositoryService : IAddonRepositoryService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly string _mainRepository;

        public AddonRepositoryService(IConfiguration configuration)
        {
            _mainRepository = configuration["AddonsConfiguration:MainRepository"]
                ?? throw new InvalidOperationException("AddonsConfiguration:MainRepository is not configured.");
        }

        /// <inheritdoc/>
        public async Task<string> GetRepository()
        {
            return await httpClient.GetStringAsync(_mainRepository);
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetAddonByKey(string key)
        {
            var releaseUrl = await ResolveReleaseUrl(key);

            var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
            request.Headers.UserAgent.ParseAdd("NordvikManager");
            var result = await httpClient.SendAsync(request);

            if (result.StatusCode == HttpStatusCode.OK)
                return await result.Content.ReadAsByteArrayAsync();

            throw new Exception($"Failed to download addon '{key}'. Remote returned {result.StatusCode}.");
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task<string> ResolveReleaseUrl(string key)
        {
            var repoJson = await GetRepository();
            var jObject = JObject.Parse(repoJson);
            var repository = jObject["repository"]
                ?? throw new Exception("Repository JSON is missing the 'repository' array.");

            var addon = repository.FirstOrDefault(x => x["key"]?.ToString() == key)
                ?? throw new Exception($"Addon with key '{key}' was not found in the repository.");

            var releaseUrl = addon["releaseUrl"]?.ToString()
                ?? throw new Exception($"Addon '{key}' has no releaseUrl defined.");

            return releaseUrl;
        }
    }
}
