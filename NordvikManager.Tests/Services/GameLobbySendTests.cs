using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Extensions;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // Sending must never take anything down with it: not the broadcast (a player joining
    // half-way, one broken connection), and not the server (an exception out of what used
    // to be an async void lambda ends the process).
    public class GameLobbySendTests
    {
        private sealed class Recording : IPlayerConnection
        {
            public List<object> Sent { get; } = new();
            public Action OnSend { get; set; }
            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(message);
                OnSend?.Invoke();
                return Task.FromResult(true);
            }
        }

        private sealed class Throwing : IPlayerConnection
        {
            public Task<bool> SendMessageToPlayer(object message) => throw new InvalidOperationException("broken");
        }

        private sealed class Faulting : IPlayerConnection
        {
            public Task<bool> SendMessageToPlayer(object message) => Task.FromException<bool>(new InvalidOperationException("broken"));
        }

        private sealed class NeverFinishing : IPlayerConnection
        {
            public Task<bool> SendMessageToPlayer(object message) => new TaskCompletionSource<bool>().Task;
        }

        private readonly PlayerDTO _sender = new() { Id = Guid.NewGuid(), Name = "Sender" };
        private readonly PlayerDTO _other = new() { Id = Guid.NewGuid(), Name = "Other" };
        private readonly GameLobby _lobby;

        public GameLobbySendTests()
        {
            var scopes = new ServiceCollection()
                .AddSingleton(new Mock<IMediator>().Object)
                .AddSingleton(new Mock<IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid() };
        }

        private void Connect(PlayerDTO player, params IPlayerConnection[] connections) =>
            _lobby.ConnectedPlayers[player] = new List<IPlayerConnection>(connections);

        private static WebSocketCommand Command() =>
            new() { Command = "anything", Result = WebSocketCommandNames.ResultOk, Data = JToken.FromObject("x") };

        [Fact]
        public async Task HandlePostCommand_StillReachesEveryone_WhenAPlayerJoinsHalfWayThrough()
        {
            var last = new Recording();
            var first = new Recording
            {
                OnSend = () => _lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid() }] = new List<IPlayerConnection>(),
            };
            Connect(_sender, first);
            Connect(_other, last);

            await _lobby.HandlePostCommand(_sender, Command());

            Assert.Single(last.Sent);
        }

        [Fact]
        public void Broadcast_ReachesTheRest_PastAConnectionThatThrows()
        {
            var fine = new Recording();
            Connect(_sender, new Recording());
            Connect(_other, new Throwing(), fine);

            _lobby.Broadcast("hello", _sender);

            Assert.Single(fine.Sent);
        }

        [Fact]
        public void SendToPlayer_ReachesTheRest_PastAConnectionThatFaults()
        {
            var fine = new Recording();
            Connect(_other, new Faulting(), fine);

            _lobby.SendToPlayer("hello", _other);

            Assert.Single(fine.Sent);
        }

        [Fact]
        public void SendToPlayer_DoesNothing_ForAPlayerWhoHasLeft()
        {
            _lobby.SendToPlayer("hello", _other);
        }

        [Fact]
        public void SendingToAPlayer_DoesNotWait_ForTheirSlowConnection()
        {
            // A large message's task completes only when all of it is sent. Their second tab
            // still gets it at once.
            var second = new Recording();
            var connections = new List<IPlayerConnection> { new NeverFinishing(), second };

            _ = connections.SendMessageToPlayer("hello");

            Assert.Single(second.Sent);
        }

        [Fact]
        public async Task HandlePostCommand_DoesNotFail_ForASenderWhoHasLeft()
        {
            var other = new Recording();
            Connect(_other, other);

            await _lobby.HandlePostCommand(_sender, new WebSocketCommand { Command = WebSocketCommandNames.CmdDebugModeGet });

            Assert.Empty(other.Sent);
        }
    }
}
