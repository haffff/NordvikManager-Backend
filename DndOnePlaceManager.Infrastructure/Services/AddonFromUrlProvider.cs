using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Settings;
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
        private static readonly HttpClient sharedHttpClient = new HttpClient();
        private readonly HttpClient httpClient;
        private readonly string _mainRepository;

        /// <summary>The largest readme served (bytes).</summary>
        public const int MaxReadmeBytes = 512 * 1024;
        private static readonly TimeSpan ReadmeTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ReadmeCacheTime = TimeSpan.FromMinutes(10);
        // Raw GitHub markdown only, as the registry schema requires: https://raw.githubusercontent.com/<owner>/<repo>/<ref>/<path>.md
        private static readonly System.Text.RegularExpressions.Regex ReadmeUrl =
            new(@"^https://raw\.githubusercontent\.com/[^/]+/[^/]+/[^/]+/.+\.md$", System.Text.RegularExpressions.RegexOptions.Compiled);
        // Per URL, so a readme moved to a new release's tag is fetched again.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Until, string Markdown)> readmeCache = new();

        public AddonRepositoryService(AddonRegistryOptions registry) : this(registry, sharedHttpClient) { }

        internal AddonRepositoryService(AddonRegistryOptions registry, HttpClient httpClient)
        {
            this.httpClient = httpClient;
            _mainRepository = registry.MainRepository
                ?? throw new InvalidOperationException("No addon registry is set: AddonsConfiguration:MainRepository in appsettings, or Application Settings.");
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

        /// <inheritdoc/>
        public async Task<AddonReadmeResult?> GetReadmeByKey(string key)
        {
            var repository = JObject.Parse(await GetRepository())["repository"];
            var url = repository?.FirstOrDefault(x => x["key"]?.ToString() == key)?["readme"]?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                return null;
            if (!ReadmeUrl.IsMatch(url))
                throw new InvalidOperationException($"The readme of '{key}' isn't a markdown file on raw.githubusercontent.com.");

            if (readmeCache.TryGetValue(url, out var cached) && cached.Until > DateTime.UtcNow)
                return new AddonReadmeResult(url, cached.Markdown);

            using var timeout = new CancellationTokenSource(ReadmeTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("NordvikManager");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"The readme of '{key}' answered {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength > MaxReadmeBytes)
                throw new InvalidOperationException($"The readme of '{key}' is over {MaxReadmeBytes / 1024} KB.");

            // Read at most one byte past the limit, whatever the server claims.
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxReadmeBytes)
                    throw new InvalidOperationException($"The readme of '{key}' is over {MaxReadmeBytes / 1024} KB.");
            }

            var markdown = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            readmeCache[url] = (DateTime.UtcNow + ReadmeCacheTime, markdown);
            return new AddonReadmeResult(url, markdown);
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
