using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.Wasm.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WasmHost
{
    // The browser host's addon registry is its Central (/api/addons): the host build's page may only
    // connect to Central's origin, and GitHub's release downloads don't allow other origins anyway.
    [Collection("ActionScenarios")]
    public class CentralAddonRepositoryTests
    {
        private static readonly byte[] PollZip = { 0x50, 0x4b, 0x03, 0x04, 1, 2, 3 };

        /// <summary>Central's addon routes; the package needs a fresh token (access-1 has expired).</summary>
        private sealed class FakeCentral : HttpMessageHandler
        {
            public List<string> Seen { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                var path = request.RequestUri!.AbsolutePath;
                Seen.Add($"{request.Method} {path} {request.Headers.Authorization}");
                HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
                    new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

                return Task.FromResult(path switch
                {
                    "/api/addons" => Json("""
                        { "categories": [{ "key": "tools", "name": "Tools" }],
                          "addons": [{ "key": "poll", "name": "Poll", "version": "0.1.0", "categories": ["tools"],
                                       "releaseUrl": "https://github.com/o/r/releases/download/poll-0.1.0/poll.zip" }] }
                        """),
                    "/api/addons/poll/readme" => Json("""{ "key": "poll", "url": "https://raw.githubusercontent.com/o/r/t/README.md", "markdown": "# Poll" }"""),
                    "/api/addons/poll/package" when request.Headers.Authorization?.Parameter == "access-2" =>
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PollZip) },
                    "/api/addons/poll/package" => Json("""{ "error": "Invalid or expired token" }""", HttpStatusCode.Unauthorized),
                    "/api/user/refresh" => Json("""{ "accessToken": "access-2", "refreshToken": "refresh-2" }"""),
                    _ => Json("""{ "error": "no" }""", HttpStatusCode.NotFound),
                });
            }
        }

        private readonly FakeCentral _central = new();
        private readonly HeldCentralTokens _tokens = new();

        private CentralAddonRepository Repository()
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_central));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CentralServerUrl"] = "http://central.test" })
                .Build();
            _tokens.Set(new CentralTokens("access-1", "refresh-1"));
            return new CentralAddonRepository(new CentralServerService(factory.Object, _tokens, config), _tokens);
        }

        [Fact]
        public async Task GetRepository_IsCentralsRegistry_InTheRegistrysOwnShape()
        {
            var repository = JObject.Parse(await Repository().GetRepository());

            // addons.json's shape, which the repository and categories handlers read
            Assert.Equal("poll", repository["repository"]![0]!["key"]!.ToString());
            Assert.Equal("https://github.com/o/r/releases/download/poll-0.1.0/poll.zip", repository["repository"]![0]!["releaseUrl"]!.ToString());
            Assert.Equal("tools", repository["categories"]![0]!["key"]!.ToString());
            Assert.Contains("GET /api/addons ", _central.Seen);
        }

        [Fact]
        public async Task GetReadmeByKey_IsCentrals_AndNullWhenItHasNone()
        {
            var repository = Repository();

            var readme = await repository.GetReadmeByKey("poll");

            Assert.Equal(new AddonReadmeResult("https://raw.githubusercontent.com/o/r/t/README.md", "# Poll"), readme);
            Assert.Null(await repository.GetReadmeByKey("nope"));
        }

        [Fact]
        public async Task GetAddonByKey_DownloadsThroughCentral_RefreshingAnExpiredToken()
        {
            var bytes = await Repository().GetAddonByKey("poll");

            Assert.Equal(PollZip, bytes);
            Assert.Equal(new[] { "GET /api/addons/poll/package Bearer access-1", "POST /api/user/refresh ", "GET /api/addons/poll/package Bearer access-2" },
                _central.Seen.Where(s => !s.StartsWith("GET /api/addons ")).ToArray());
            Assert.Equal("refresh-2", _tokens.RefreshToken);
        }

        [Fact]
        public async Task GetAddonByKey_SaysWhyWhenCentralRefuses()
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => Repository().GetAddonByKey("nope"));

            Assert.Contains("nope", error.Message);
        }

        [Fact]
        public async Task WasmHost_UsesCentralAsItsAddonRegistry()
        {
            await using var provider = NordvikManager.Wasm.Hosting.WasmHost.Build(
                NordvikManager.Wasm.Hosting.WasmHost.Configuration("https://central.example"), new WasmHostTests.NoBrowserRtc());

            using var scope = provider.CreateScope();
            Assert.IsType<CentralAddonRepository>(scope.ServiceProvider.GetRequiredService<IAddonRepositoryService>());
            // A registry is set: the lobby's featured addons and new games may use it.
            Assert.Equal("https://central.example/api/addons", provider.GetRequiredService<AddonRegistryOptions>().MainRepository);
        }
    }
}
