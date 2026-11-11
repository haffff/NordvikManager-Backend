using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Newtonsoft.Json.Linq;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Interfaces;
using NordvikManager.Wasm.Hosting;
using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;
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
    // The browser host's own GM, in its own tab: logs in to Central (tokens held by the host,
    // no cookies), and runs the same endpoints as everyone (game list, create game, start a
    // session) with those tokens as its cookies. A stub Central and fake signaling stand in.
    [Collection("ActionScenarios")] // WasmHost.Build sets PermissionsExtension.ServiceProvider
    public class HostGmTests : IAsyncLifetime
    {
        private readonly StubCentral _central = new();
        private readonly Mock<ISignalingService> _signaling = new();
        private readonly ServiceProvider _provider;
        private readonly HostGm _host;
        private readonly string _gmId = Guid.NewGuid().ToString();

        public HostGmTests()
        {
            _signaling.Setup(s => s.ConnectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _provider = NordvikManager.Wasm.Hosting.WasmHost.Build(
                // A database of its own: EF shares in-memory databases by name across a process.
                NordvikManager.Wasm.Hosting.WasmHost.Configuration("http://central.test",
                    new Dictionary<string, string?> { ["ConnectionStrings:DBData"] = "host-gm-" + Guid.NewGuid() }), new NoBrowserRtc(), services =>
                {
                    var factory = new Mock<IHttpClientFactory>();
                    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_central));
                    services.Replace(ServiceDescriptor.Singleton(factory.Object));
                    services.Replace(ServiceDescriptor.Singleton(_signaling.Object));
                });
            _host = _provider.GetRequiredService<HostGm>();
            _central.Handle = Central;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync() => await _provider.DisposeAsync();

        private static string Jwt(string sub, string name, DateTime expires)
        {
            static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var exp = new DateTimeOffset(expires).ToUnixTimeSeconds();
            return B64("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." + B64($"{{\"sub\":\"{sub}\",\"username\":\"{name}\",\"exp\":{exp}}}") + ".";
        }

        private string AccessToken(int n) => Jwt(_gmId, "gm", DateTime.UtcNow.AddMinutes(n == 0 ? -5 : 15)) + n;

        private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json") };

        // A Central that knows the GM, gives sessions and ICE servers, and refreshes refresh-1 into access 2.
        private HttpResponseMessage Central(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
        {
            "/api/user/login" => Json(new { accessToken = AccessToken(1), refreshToken = "refresh-1" }),
            "/api/user/refresh" => Json(new { accessToken = AccessToken(2), refreshToken = "refresh-2" }),
            "/api/gamelist/addgame" => Json(new { id = "central-session-1" }),
            "/api/gamelist/join" => Json(new { ok = true }),
            "/api/ice-servers" => Json(new { iceServers = Array.Empty<object>(), ttl = 60 }),
            "/api/gamelist/signalingticket" => Json(new { ticket = "ticket-1" }),
            _ => Json(new { }),
        };

        [Fact]
        public async Task Login_HoldsCentralsTokens_AndMakesTheGmTheHostsLocalAdmin()
        {
            Assert.True(await _host.LoginAsync("gm", "pw"));

            var tokens = _provider.GetRequiredService<HeldCentralTokens>();
            Assert.Equal(AccessToken(1), tokens.AccessToken);
            Assert.Equal("refresh-1", tokens.RefreshToken);
            var localAdmin = _provider.GetRequiredService<ILocalAdminService>();
            Assert.True(await localAdmin.IsLocalAdminAsync(_gmId, null));
            Assert.False(await localAdmin.IsLocalAdminAsync(Guid.NewGuid().ToString(), null));
        }

        [Fact]
        public async Task BeforeLogin_NobodyIsLocalAdmin_AndCallsAre401()
        {
            Assert.False(await _provider.GetRequiredService<ILocalAdminService>().IsLocalAdminAsync(_gmId, null));
            Assert.Equal(401, (await _host.CallAsync("GET", "api/GameList/GetGames")).Status);
        }

        [Fact]
        public async Task AddGame_CreatesTheCentralSession_WithTheHeldToken_AndLists()
        {
            await _host.LoginAsync("gm", "pw");

            var (status, _) = await _host.CallAsync("POST", "api/GameList/AddGame", JObject.FromObject(new { name = "Tab game", passwordRequired = false }));
            var (listed, games) = await _host.CallAsync("GET", "api/GameList/GetGames");

            Assert.Equal(200, status);
            var create = _central.Requests.Single(r => r.Path == "/api/gamelist/addgame");
            Assert.Equal("Bearer " + AccessToken(1), create.Authorization);
            Assert.Equal(200, listed);
            Assert.Contains(games!, g => (string?)g["name"] == "Tab game");
        }

        [Fact]
        public async Task StartSession_ConnectsSignaling_ForTheGamesCentralSession()
        {
            await _host.LoginAsync("gm", "pw");
            await _host.CallAsync("POST", "api/GameList/AddGame", JObject.FromObject(new { name = "Tab game", passwordRequired = false }));
            var gameId = (string)(await _host.CallAsync("GET", "api/GameList/GetGames")).Body![0]!["id"]!;

            var (status, body) = await _host.CallAsync("POST", $"api/Session/{gameId}/start");

            Assert.True(status == 200, $"{status} {body}");
            Assert.Equal("central-session-1", (string?)body!["centralSessionId"]);
            _signaling.Verify(s => s.ConnectAsync(gameId, "central-session-1", "ticket-1"), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AnEndpointsRefresh_IsWrittenBackToTheHeldTokens()
        {
            // Login hands out an access token that's already expired: starting a session refreshes it.
            _central.Handle = r => r.RequestUri!.AbsolutePath == "/api/user/login"
                ? Json(new { accessToken = AccessToken(0), refreshToken = "refresh-1" })
                : Central(r);
            await _host.LoginAsync("gm", "pw");
            await _host.CallAsync("POST", "api/GameList/AddGame", JObject.FromObject(new { name = "Tab game", passwordRequired = false }));

            var tokens = _provider.GetRequiredService<HeldCentralTokens>();
            Assert.Equal(AccessToken(2), tokens.AccessToken);
            Assert.Equal("refresh-2", tokens.RefreshToken);
        }

        // With the host GM logged in, a player's tunnelled request still has no Central token:
        // it must not be refreshed into the GM's.
        [Fact]
        public async Task ATunnelledRequest_DoesNotGetTheHostGmsCentralLogin()
        {
            await _host.LoginAsync("gm", "pw");
            _central.Requests.Clear();
            _central.Handle = r => r.RequestUri!.AbsolutePath == "/api/user/KeyboardBindings"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Central(r);
            var connection = new RecordingConnection();

            await _provider.GetRequiredService<IWebRTCApiDispatcher>().DispatchAsync(
                new WebRTCApiRequest { Id = "req-1", Method = "GET", Path = "/api/user/KeyboardBindings" },
                new PlayerDTO { Id = Guid.NewGuid(), Name = "Mallory", CentralServerUserId = Guid.NewGuid().ToString() }, Guid.NewGuid(), connection);

            Assert.DoesNotContain(_central.Requests, r => r.Path == "/api/user/refresh");
            Assert.DoesNotContain(_central.Requests, r => r.Authorization?.Contains(AccessToken(1)) == true);
        }

        private sealed class StubCentral : HttpMessageHandler
        {
            public List<(string Path, string? Authorization)> Requests { get; } = new();
            public Func<HttpRequestMessage, HttpResponseMessage> Handle { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                lock (Requests)
                    Requests.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString()));
                return Task.FromResult(Handle(request));
            }
        }

        private sealed class RecordingConnection : IPlayerConnection
        {
            public List<object> Sent { get; } = new();

            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(message);
                return Task.FromResult(true);
            }
        }

        private sealed class NoBrowserRtc : IBrowserRtc
        {
            public void CreatePeer(string peerId, string iceServersJson) { }
            public Task<string?> SetRemoteOfferAsync(string peerId, string sdp) => Task.FromResult<string?>(null);
            public Task<string> CreateAnswerAsync(string peerId) => Task.FromResult("answer");
            public void AddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex) { }
            public void ClosePeer(string peerId) { }
            public void Send(string channelId, string message) { }
            public double BufferedAmount(string channelId) => 0;
        }
    }
}
