using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Tests.Services
{
    // A whisper or GM roll (ChatHandler sets VisibleTo) must reach only those players'
    // connections; ordinary chat still reaches everyone.
    public class GameLobbyChatPrivacyTests
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
        private readonly PlayerDTO _alice = new() { Id = Guid.NewGuid(), Name = "Alice" };
        private readonly PlayerDTO _bob = new() { Id = Guid.NewGuid(), Name = "Bob" };
        private readonly PlayerDTO _eve = new() { Id = Guid.NewGuid(), Name = "Eve" };
        private readonly Dictionary<PlayerDTO, RecordingConnection> _connections = new();
        private readonly GameLobby _lobby;

        public GameLobbyChatPrivacyTests()
        {
            var scopes = new ServiceCollection()
                .AddSingleton(new Mock<IMediator>().Object)
                .AddSingleton(new Mock<NordvikManager.Services.Interfaces.IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            foreach (var player in new[] { _gm, _alice, _bob, _eve })
            {
                var connection = new RecordingConnection();
                _connections[player] = connection;
                _lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
            }
        }

        private static WebSocketCommand Chat(string text, List<Guid>? visibleTo = null, Guid? recipientId = null) => new()
        {
            Command = WebSocketCommandNames.CmdChatPush,
            Data = new JValue(text),
            Result = WebSocketCommandNames.ResultOk,
            VisibleTo = visibleTo,
            RecipientId = recipientId,
        };

        private int Received(PlayerDTO player) =>
            _connections[player].Sent.Count(c => c.Command == WebSocketCommandNames.CmdChatPush);

        [Fact]
        public async Task Whisper_ReachesOnlyItsPlayers()
        {
            await _lobby.HandlePostCommand(_alice, Chat("psst", new List<Guid> { _alice.Id!.Value, _bob.Id!.Value, _gm.Id!.Value }, _bob.Id));

            Assert.Equal(1, Received(_alice));
            Assert.Equal(1, Received(_bob));
            Assert.Equal(1, Received(_gm));
            Assert.Equal(0, Received(_eve));
            Assert.Equal(_bob.Id, _connections[_bob].Sent.Single().RecipientId);
        }

        [Fact]
        public async Task PublicMessage_ReachesEveryone()
        {
            await _lobby.HandlePostCommand(_alice, Chat("hello"));

            foreach (var player in new[] { _gm, _alice, _bob, _eve })
                Assert.Equal(1, Received(player));
        }
    }
}
