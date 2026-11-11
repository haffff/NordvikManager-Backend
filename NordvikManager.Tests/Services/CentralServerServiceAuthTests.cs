using Microsoft.Extensions.Configuration;
using Moq;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // How the GM host authenticates to Central. Bearer, and tokens from the response body:
    // a browser-wasm host can neither set a Cookie header nor read Set-Cookie, and Central
    // accepts both ways (Bearer since 2026-09-06). The refresh token comes from the host
    // (ICentralTokenStore): the native server's GM cookies, the wasm host's own login.
    public class CentralServerServiceAuthTests
    {
        private sealed class StubCentral : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new();
            public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Requests.Add(request);
                return Task.FromResult(Respond(request));
            }
        }

        private readonly StubCentral _central = new();
        private readonly Mock<ICentralTokenStore> _tokens = new();
        private readonly CentralServerService _service;

        public CentralServerServiceAuthTests()
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_central));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CentralServerUrl"] = "http://central.test" })
                .Build();
            _service = new CentralServerService(factory.Object, _tokens.Object, config);
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        // A JWT with sub/username claims (unsigned: the service only reads them).
        private static string Jwt(string sub, string name)
        {
            static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return B64("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." + B64($"{{\"sub\":\"{sub}\",\"username\":\"{name}\"}}") + ".";
        }

        [Fact]
        public async Task AuthenticatedCall_SendsBearer_NotACookie()
        {
            _central.Respond = _ => Json("{\"iceServers\":[],\"ttl\":60}");

            await _service.GetIceServersAsync("access-1");

            var request = _central.Requests.Single();
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-1", request.Headers.Authorization?.Parameter);
            Assert.False(request.Headers.Contains("Cookie"));
        }

        [Fact]
        public async Task Login_ReadsTheTokensFromTheBody()
        {
            var access = Jwt("user-1", "alice");
            _central.Respond = _ => Json($"{{\"accessToken\":\"{access}\",\"refreshToken\":\"refresh-1\"}}");

            var login = await _service.LoginAsync("alice", "pw");

            Assert.Equal(access, login!.CentralToken);
            Assert.Equal("refresh-1", login.RefreshToken);
            Assert.Equal("user-1", login.UserId);
            Assert.Equal("alice", login.UserName);
        }

        [Fact]
        public async Task Login_WithTheAccessTokenOnlyInACookie_StillWorks()
        {
            var access = Jwt("user-1", "alice");
            _central.Respond = _ =>
            {
                var response = Json("{\"refreshToken\":\"refresh-1\"}");
                response.Headers.Add("Set-Cookie", $"Authorization={access}; Path=/; HttpOnly");
                return response;
            };

            var login = await _service.LoginAsync("alice", "pw");

            Assert.Equal(access, login!.CentralToken);
        }

        [Fact]
        public async Task Unauthorized_RefreshesWithTheHostsToken_HandsBackTheNewOnes_AndRetries()
        {
            _tokens.Setup(t => t.RefreshToken).Returns("refresh-0");
            _central.Respond = request => request.RequestUri!.AbsolutePath switch
            {
                "/api/user/refresh" => Json("{\"accessToken\":\"access-2\",\"refreshToken\":\"refresh-2\"}"),
                _ when request.Headers.Authorization?.Parameter == "access-2" => Json("{\"iceServers\":[],\"ttl\":60}"),
                _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            };

            var result = await _service.GetIceServersAsync("access-1");

            Assert.NotNull(result);
            _tokens.Verify(t => t.Refreshed(new CentralTokens("access-2", "refresh-2")), Times.Once);
            Assert.Equal(new[] { "/api/ice-servers", "/api/user/refresh", "/api/ice-servers" },
                _central.Requests.Select(r => r.RequestUri!.AbsolutePath));
        }

        [Fact]
        public async Task Unauthorized_WithoutARefreshToken_GivesUp()
        {
            _central.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

            Assert.Null(await _service.GetIceServersAsync("access-1"));
            Assert.Single(_central.Requests);
            _tokens.Verify(t => t.Refreshed(It.IsAny<CentralTokens>()), Times.Never);
        }
    }
}
