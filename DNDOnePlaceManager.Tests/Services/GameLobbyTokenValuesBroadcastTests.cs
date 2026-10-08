using DndOnePlaceManager.Application.Commands.Properties.GetTokenViewers;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Services
{
    // A change to a card's value reaches players who can read the card, and also those
    // who can only see a token placed for it that shows that value (its bars).
    public class GameLobbyTokenValuesBroadcastTests
    {
        private sealed class RecordingConnection : IPlayerConnection
        {
            public List<object> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message) { Sent.Add(message); return Task.FromResult(true); }
        }

        private readonly Mock<IMediator> _mediator = new();
        private readonly Guid _cardId = Guid.NewGuid();
        private readonly PlayerDTO _owner = new() { Id = Guid.NewGuid(), Name = "Owner" };
        private readonly PlayerDTO _viewer = new() { Id = Guid.NewGuid(), Name = "Sees the token" };
        private readonly PlayerDTO _stranger = new() { Id = Guid.NewGuid(), Name = "Sees nothing" };
        private readonly Dictionary<PlayerDTO, RecordingConnection> _connections = new();
        private readonly GameLobby _lobby;

        public GameLobbyTokenValuesBroadcastTests()
        {
            // Only the owner can read the card.
            _mediator.Setup(m => m.Send(It.IsAny<GetPermissionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Dictionary<Guid, Permission> { [_owner.Id!.Value] = Permission.All });
            _mediator.Setup(m => m.Send(It.IsAny<GetTokenViewersCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((GetTokenViewersCommand c, CancellationToken _) =>
                         c.PlayerIds.Where(id => id == _viewer.Id && c.PropertyName == "bar1_value").ToList());

            var scopes = new ServiceCollection()
                .AddSingleton(_mediator.Object)
                .AddSingleton(new Mock<DNDOnePlaceManager.Services.Interfaces.IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            foreach (var player in new[] { _owner, _viewer, _stranger })
            {
                var connection = new RecordingConnection();
                _connections[player] = connection;
                _lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
            }
        }

        private WebSocketCommand Change(string command, string name) => new()
        {
            Command = command,
            Result = WebSocketCommandNames.ResultOk,
            Data = new JObject { ["parentId"] = _cardId.ToString(), ["name"] = name, ["value"] = "5" },
        };

        private bool Received(PlayerDTO player) => _connections[player].Sent.Count > 0;

        [Fact]
        public async Task PropertyUpdate_ShownByAVisibleToken_ReachesItsViewer()
        {
            await _lobby.HandlePostCommand(_owner, Change(WebSocketCommandNames.PropertyUpdate, "bar1_value"));

            Assert.True(Received(_owner));
            Assert.True(Received(_viewer));
            Assert.False(Received(_stranger));
            _mediator.Verify(m => m.Send(It.Is<GetTokenViewersCommand>(c =>
                c.CardId == _cardId && c.PropertyName == "bar1_value" && c.GameId == _lobby.GameId
                && c.PlayerIds.Count == 2 && c.PlayerIds.Contains(_viewer.Id!.Value) && c.PlayerIds.Contains(_stranger.Id!.Value)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PropertyUpdate_NotShownByAnyToken_StaysWithTheCardsReaders()
        {
            await _lobby.HandlePostCommand(_owner, Change(WebSocketCommandNames.PropertyUpdate, "secret_backstory"));

            Assert.True(Received(_owner));
            Assert.False(Received(_viewer));
            Assert.False(Received(_stranger));
        }

        [Fact]
        public async Task OtherCommands_DoNotLookForTokenViewers()
        {
            await _lobby.HandlePostCommand(_owner, Change("card_update", "bar1_value"));

            Assert.False(Received(_viewer));
            _mediator.Verify(m => m.Send(It.IsAny<GetTokenViewersCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
