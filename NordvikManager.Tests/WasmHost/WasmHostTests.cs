using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.Wasm.Hosting;
using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets.Handlers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WasmHost
{
    // The browser-wasm GM host's composition, built on the desktop: every registration resolves
    // (ValidateOnBuild/ValidateScopes), with the browser's WebRTC faked. The same composition
    // runs in a browser in NordvikManager.Wasm.Host's self-check.
    // In the ActionScenarios collection: building it sets PermissionsExtension.ServiceProvider
    // (ApplicationLayerModule.AfterBuild), a process-wide static those tests set too.
    [Collection("ActionScenarios")]
    public class WasmHostTests : IAsyncLifetime
    {
        private readonly ServiceProvider _provider = NordvikManager.Wasm.Hosting.WasmHost.Build(
            NordvikManager.Wasm.Hosting.WasmHost.Configuration("http://central.test"), new NoBrowserRtc());

        public Task InitializeAsync() => Task.CompletedTask;

        // SignalingService is only IAsyncDisposable.
        public async Task DisposeAsync() => await _provider.DisposeAsync();

        private static Type[] Concrete<T>() => typeof(T).Assembly.GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToArray();

        // The host build is served by Central itself: a cookie Central set on the host's requests
        // would be every tab's on that origin (a player client tab would be the GM, and its refresh
        // would rotate the token the host holds). The host's requests carry none and keep none.
        [Fact]
        public async Task HttpClients_AskTheBrowserForNoCookies()
        {
            var seen = new System.Collections.Generic.List<System.Net.Http.HttpRequestMessage>();
            await using var provider = NordvikManager.Wasm.Hosting.WasmHost.Build(
                NordvikManager.Wasm.Hosting.WasmHost.Configuration("http://central.test"), new NoBrowserRtc(),
                services => services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => new Capture(seen))));
            var factory = provider.GetRequiredService<System.Net.Http.IHttpClientFactory>();

            await factory.CreateClient().GetAsync("http://central.test/api/user/userinfo");
            await factory.CreateClient("any").GetAsync("http://central.test/api/meta");

            Assert.Equal(2, seen.Count);
            Assert.All(seen, request =>
            {
                Assert.True(request.Options.TryGetValue(NoBrowserCookies.FetchOptions, out var options));
                Assert.Equal("omit", options["credentials"]);
            });
        }

        private sealed class Capture : System.Net.Http.HttpMessageHandler
        {
            private readonly System.Collections.Generic.List<System.Net.Http.HttpRequestMessage> _seen;
            public Capture(System.Collections.Generic.List<System.Net.Http.HttpRequestMessage> seen) => _seen = seen;

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                _seen.Add(request);
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }

        [Fact]
        public void Build_ResolvesTheSessionService_EveryHandler_AndEveryStep()
        {
            Assert.IsType<WebRTCSessionService>(_provider.GetRequiredService<IWebRTCSessionService>());
            using var scope = _provider.CreateScope();
            Assert.Equal(Concrete<IWebSocketHandler>().Length, scope.ServiceProvider.GetServices<IWebSocketHandler>().Count());
            Assert.Equal(Concrete<IActionStepDefinition>().Length, _provider.GetServices<IActionStepDefinition>().Count());
        }

        [Fact]
        public void Build_UsesTheBrowsersWebRtc_ItsOwnCentralTokens_AndWebSocketSignaling()
        {
            Assert.IsType<BrowserPeerConnectionFactory>(_provider.GetRequiredService<IPeerConnectionFactory>());
            Assert.Same(_provider.GetRequiredService<HeldCentralTokens>(), _provider.GetRequiredService<ICentralTokenStore>());
            Assert.Equal(SocketIOClient.Common.TransportProtocol.WebSocket,
                SignalingService.BuildOptions(_provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()).Transport);
        }

        [Fact]
        public async Task Mediator_CreatesAndListsAGame()
        {
            using var scope = _provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };

            var gameId = await mediator.Send(new AddGameCommand { User = gm, Name = "Browser game", PasswordRequired = false });
            var list = await mediator.Send(new GetGameListCommand { UserId = gm.Id });

            Assert.NotNull(gameId);
            Assert.Contains(list.GameItemList!, g => g.Name == "Browser game");
        }

        // A browser host has no settings file: its upload limits are fixed, and can't be changed.
        [Fact]
        public void ServerSettings_AreFixed_WithTheBrowserHostsUploadLimits()
        {
            var store = _provider.GetRequiredService<NordvikManager.Infrastructure.Interfaces.IServerSettingsStore>();

            Assert.IsType<FixedServerSettingsStore>(store);
            Assert.Equal(FixedServerSettingsStore.GmGameLimitMb, store.Load().Uploads.GmGameLimitMb);
            Assert.Equal(FixedServerSettingsStore.PlayerLimitMb, store.Load().Uploads.PlayerLimitMb);
            Assert.ThrowsAny<Exception>(() => store.Update(s => s.Uploads.PlayerLimitMb = 0));
            Assert.Equal(FixedServerSettingsStore.PlayerLimitMb, store.Load().Uploads.PlayerLimitMb);
        }

        // Materials through the browser host's tunnel, real handlers: a file uploaded in parts,
        // then read back by api/materials/resource (rewritten to its base64 variant, as natively).
        [Fact]
        public async Task ApiDispatcher_UploadsAResourceInParts_AndReadsItBack()
        {
            Guid gameId;
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
            using (var scope = _provider.CreateScope())
                gameId = (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                    new AddGameCommand { User = gm, Name = "Upload game", PasswordRequired = false }))!.Value;
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "gm", CentralServerUserId = gm.Id };
            var bytes = System.Text.Encoding.UTF8.GetBytes("a small map, but a map");

            var start = await Tunnel("/api/materials/UploadStart", caller, gameId, "POST",
                Newtonsoft.Json.Linq.JObject.FromObject(new { name = "map.png", mimeType = "image/png", size = bytes.Length }));
            Assert.Equal(200, (int?)start["status"]);
            var uploadId = (string?)start["body"]?["uploadId"];
            var part = await Tunnel("/api/materials/UploadPart", caller, gameId, "POST",
                Newtonsoft.Json.Linq.JObject.FromObject(new { uploadId, offset = 0, data = Convert.ToBase64String(bytes) }));
            Assert.Equal(200, (int?)part["status"]);
            var finish = await Tunnel("/api/materials/UploadFinish", caller, gameId, "POST",
                Newtonsoft.Json.Linq.JObject.FromObject(new { uploadId }));
            Assert.Equal(200, (int?)finish["status"]);
            var id = (string?)finish["body"]?["id"];

            var read = await Tunnel("/api/materials/resource", caller, gameId, query: new() { ["id"] = id! });

            Assert.Equal(200, (int?)read["status"]);
            Assert.Equal(Convert.ToBase64String(bytes), (string?)read["body"]?["data"]);
        }

        [Fact]
        public async Task ApiDispatcher_DiskLinking_IsNativeOnly()
        {
            var json = await Tunnel("/api/materials/BrowseLocalDirectory", new PlayerDTO { Id = Guid.NewGuid(), Name = "gm" }, Guid.NewGuid());

            Assert.Equal(501, (int?)json["status"]);
            Assert.Equal(NordvikManager.Api.ApiRouter.NativeOnlyMessage, (string?)json["body"]?["error"]);
        }

        private async Task<Newtonsoft.Json.Linq.JObject> Tunnel(string path, PlayerDTO player, Guid gameId, string method = "GET",
            Newtonsoft.Json.Linq.JToken? body = null, System.Collections.Generic.Dictionary<string, string>? query = null)
        {
            var connection = new RecordingConnection();
            await _provider.GetRequiredService<IWebRTCApiDispatcher>().DispatchAsync(
                new WebRTCApiRequest { Id = "req-1", Method = method, Path = path, Body = body, Query = query }, player, gameId, connection);
            var json = Newtonsoft.Json.Linq.JObject.FromObject(connection.Sent.Single());
            Assert.Equal("api-response", (string?)json["type"]);
            Assert.Equal("req-1", (string?)json["id"]);
            return json;
        }

        [Fact]
        public async Task ApiDispatcher_IsTheSharedRouter_And501sServerOnlyRoutes()
        {
            Assert.IsType<NordvikManager.Api.ApiRouter>(_provider.GetRequiredService<IWebRTCApiDispatcher>());

            var json = await Tunnel("/api/meta", new PlayerDTO { Id = Guid.NewGuid(), Name = "Alice" }, Guid.NewGuid());

            Assert.Equal(501, (int?)json["status"]);
            Assert.Equal(NordvikManager.Api.ApiRouter.NativeOnlyMessage, (string?)json["body"]?["error"]);
        }

        [Fact]
        public async Task ApiDispatcher_AnswersGetPlayerAndGetFullGame_FromTheRealHandlers()
        {
            Guid gameId;
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
            using (var scope = _provider.CreateScope())
                gameId = (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                    new AddGameCommand { User = gm, Name = "Tunnel game", PasswordRequired = false }))!.Value;
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "gm", CentralServerUserId = gm.Id };

            var player = await Tunnel("/api/battlemap/GetPlayer", caller, gameId);
            var game = await Tunnel("/api/battlemap/GetFullGame", caller, gameId);

            Assert.Equal(200, (int?)player["status"]);
            Assert.Equal("Game Master", (string?)player["body"]?["name"]);
            Assert.Equal(200, (int?)game["status"]);
            Assert.Equal("Tunnel game", (string?)game["body"]?["name"]);
        }

        private sealed class RecordingConnection : IPlayerConnection
        {
            public System.Collections.Generic.List<object> Sent { get; } = new();

            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(message);
                return Task.FromResult(true);
            }
        }

        // What the browser host tells its GM it doesn't provide, pinned: changing it is a decision
        // (it's shown to GMs, and in the docs). UPDATE_HOST_INFO_SNAPSHOT=1 rewrites the file.
        [Fact]
        public async Task HostInfo_IsTheBrowserHosts_AsPinnedInItsSnapshot()
        {
            Assert.IsType<BrowserHostDescription>(_provider.GetRequiredService<NordvikManager.Api.IHostDescription>());
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };

            var (status, body) = await _provider.GetRequiredService<NordvikManager.Api.ApiRouter>()
                .InvokeAsync(new NordvikManager.Api.ApiContext(gm, null), "GET", "api/Host/Info");

            Assert.Equal(200, status);
            Assert.Equal("browser", (string?)body!["kind"]);
            body["version"] = "<version>"; // the build's
            var actual = Lf(body.ToString(Newtonsoft.Json.Formatting.Indented)) + "\n";
            var file = System.IO.Path.Combine(SnapshotDir(), "host-info.browser.json");
            if (Environment.GetEnvironmentVariable("UPDATE_HOST_INFO_SNAPSHOT") == "1")
            {
                System.IO.Directory.CreateDirectory(SnapshotDir());
                System.IO.File.WriteAllText(file, actual);
            }
            Assert.True(System.IO.File.Exists(file), $"No snapshot {file}; run with UPDATE_HOST_INFO_SNAPSHOT=1 to create it.");
            Assert.Equal(Lf(System.IO.File.ReadAllText(file)), actual);
        }

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        private static string SnapshotDir([System.Runtime.CompilerServices.CallerFilePath] string here = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, "Snapshots");

        internal sealed class NoBrowserRtc : IBrowserRtc
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
