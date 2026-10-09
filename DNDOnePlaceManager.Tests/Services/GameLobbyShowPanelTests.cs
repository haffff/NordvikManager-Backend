using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Services
{
    // The battle map's "Show" menu sends show_panel; no handler claimed it, so the lobby
    // answered "command not found" to the GM and no player ever saw the battle map.
    public class GameLobbyShowPanelTests
    {
        private sealed class RecordingConnection : IPlayerConnection
        {
            public List<WebSocketCommand> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message)
            {
                if (message is WebSocketCommand command) Sent.Add(command);
                return Task.FromResult(true);
            }
        }

        private readonly PlayerDTO _gm = new() { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
        private readonly PlayerDTO _abelard = new() { Id = Guid.NewGuid(), Name = "Abelard" };
        private readonly PlayerDTO _other = new() { Id = Guid.NewGuid(), Name = "Other" };
        private readonly Dictionary<PlayerDTO, RecordingConnection> _connections = new();
        private readonly GameLobby _lobby;

        public GameLobbyShowPanelTests()
        {
            var scopes = new ServiceCollection()
                .AddSingleton(new Mock<IMediator>().Object)
                .AddSingleton(new Mock<DNDOnePlaceManager.Services.Interfaces.IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            foreach (var player in new[] { _gm, _abelard, _other })
            {
                var connection = new RecordingConnection();
                _connections[player] = connection;
                _lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
            }
        }

        private static WebSocketCommand ShowBattleMap(Guid? targetPlayerId)
        {
            var data = new JObject { ["type"] = "Battlemap", ["syncId"] = Guid.NewGuid().ToString() };
            if (targetPlayerId != null) data["targetPlayerId"] = targetPlayerId.ToString();
            return new WebSocketCommand { Command = WebSocketCommandNames.CmdShowPanel, Data = data };
        }

        private List<WebSocketCommand> Panels(PlayerDTO player) =>
            _connections[player].Sent.Where(c => c.Command == WebSocketCommandNames.CmdShowPanel).ToList();

        [Fact]
        public async Task ShowPanel_ReachesEveryOtherPlayer_WhenGmShowsToAll()
        {
            await _lobby.HandlePostCommand(_gm, ShowBattleMap(null));

            Assert.Single(Panels(_abelard));
            Assert.Single(Panels(_other));
            Assert.Empty(Panels(_gm));
        }

        [Fact]
        public async Task ShowPanel_ReachesOnlyTheTarget_WhenGmShowsToOnePlayer()
        {
            await _lobby.HandlePostCommand(_gm, ShowBattleMap(_abelard.Id));

            Assert.Single(Panels(_abelard));
            Assert.Empty(Panels(_other));
        }

        [Fact]
        public async Task ShowPanel_IsRefused_WhenSentByAPlayer()
        {
            await _lobby.HandlePostCommand(_abelard, ShowBattleMap(null));

            Assert.Empty(Panels(_gm));
            Assert.Empty(Panels(_other));
        }
    }
}
