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

        private async Task<Newtonsoft.Json.Linq.JObject> Tunnel(string path, PlayerDTO player, Guid gameId)
        {
            var connection = new RecordingConnection();
            await _provider.GetRequiredService<IWebRTCApiDispatcher>().DispatchAsync(
                new WebRTCApiRequest { Id = "req-1", Method = "GET", Path = path }, player, gameId, connection);
            var json = Newtonsoft.Json.Linq.JObject.FromObject(connection.Sent.Single());
            Assert.Equal("api-response", (string?)json["type"]);
            Assert.Equal("req-1", (string?)json["id"]);
            return json;
        }

        [Fact]
        public async Task ApiDispatcher_IsTheSharedRouter_And501sWhatIsNotMovedYet()
        {
            Assert.IsType<NordvikManager.Api.ApiRouter>(_provider.GetRequiredService<IWebRTCApiDispatcher>());

            var json = await Tunnel("/api/user/userinfo", new PlayerDTO { Id = Guid.NewGuid(), Name = "Alice" }, Guid.NewGuid());

            Assert.Equal(501, (int?)json["status"]);
            Assert.Equal(NordvikManager.Api.ApiRouter.NotAvailableMessage, (string?)json["body"]?["error"]);
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
