using DndOnePlaceManager.Infrastructure.Services;
using DndOnePlaceManager.Infrastructure.Services.Settings;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Addons
{
    // An addon's readme: its URL only ever comes from the registry entry with that key, it must
    // be a markdown file on raw.githubusercontent.com, it stays small and is cached.
    public class AddonRepositoryReadmeTests
    {
        private const string Registry = "https://registry.example/addons.json";

        private sealed class FakeHttp : HttpMessageHandler
        {
            public readonly Dictionary<string, (HttpStatusCode Status, string Body)> Answers = new();
            public readonly List<string> Requests = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                lock (Requests) Requests.Add(url);
                var (status, body) = Answers.TryGetValue(url, out var a) ? a : (HttpStatusCode.NotFound, "");
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
            }
        }

        private static (AddonRepositoryService Service, FakeHttp Http) Service(string? readme)
        {
            var http = new FakeHttp();
            var entry = readme == null ? "{\"key\":\"poll\"}" : $"{{\"key\":\"poll\",\"readme\":\"{readme}\"}}";
            http.Answers[Registry] = (HttpStatusCode.OK, $"{{\"repository\":[{entry},{{\"key\":\"dnd5e\"}}]}}");
            var options = AddonRegistryOptions.FromConfiguration(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AddonsConfiguration:MainRepository"] = Registry }).Build());
            return (new AddonRepositoryService(options, new HttpClient(http)), http);
        }

        // A URL no other test uses, so the shared cache can't answer for it.
        private static string Url(string name) => $"https://raw.githubusercontent.com/h/Extras/{Guid.NewGuid():N}/{name}.md";

        [Fact]
        public async Task GetReadmeByKey_FetchesTheEntrysReadme()
        {
            var url = Url("README");
            var (service, http) = Service(url);
            http.Answers[url] = (HttpStatusCode.OK, "# Poll\nVote!");

            var readme = await service.GetReadmeByKey("poll");

            Assert.Equal(url, readme!.Url);
            Assert.Equal("# Poll\nVote!", readme.Markdown);
        }

        [Fact]
        public async Task GetReadmeByKey_NoReadmeOrUnknownKey_IsNull()
        {
            var (service, _) = Service(null);

            Assert.Null(await service.GetReadmeByKey("poll"));
            Assert.Null(await service.GetReadmeByKey("dnd5e"));
            Assert.Null(await service.GetReadmeByKey("nope"));
        }

        [Theory]
        [InlineData("http://raw.githubusercontent.com/h/X/t/README.md")]
        [InlineData("https://example.com/h/X/t/README.md")]
        [InlineData("https://raw.githubusercontent.com/h/X/t/notes.txt")]
        public async Task GetReadmeByKey_NotARawGitHubMarkdownFile_IsRefusedWithoutFetching(string url)
        {
            var (service, http) = Service(url);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReadmeByKey("poll"));
            Assert.DoesNotContain(url, http.Requests);
        }

        [Fact]
        public async Task GetReadmeByKey_TooLarge_IsRefused()
        {
            var url = Url("BIG");
            var (service, http) = Service(url);
            http.Answers[url] = (HttpStatusCode.OK, new string('x', AddonRepositoryService.MaxReadmeBytes + 1));

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReadmeByKey("poll"));
        }

        [Fact]
        public async Task GetReadmeByKey_Failure_SaysSo()
        {
            var (service, _) = Service(Url("MISSING"));

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReadmeByKey("poll"));
            Assert.Contains("404", e.Message);
        }

        [Fact]
        public async Task GetReadmeByKey_IsCachedPerUrl()
        {
            var url = Url("CACHED");
            var (service, http) = Service(url);
            http.Answers[url] = (HttpStatusCode.OK, "# Cached");

            await service.GetReadmeByKey("poll");
            await service.GetReadmeByKey("poll");

            Assert.Single(http.Requests, r => r == url);
        }
    }
}
