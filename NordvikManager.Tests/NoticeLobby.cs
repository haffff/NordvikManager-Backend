using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using MediatR;
using Moq;
using System.Collections.Generic;
using System.Threading;

namespace NordvikManager.Tests
{
    // A TestLobby with the game's GM (its owner) and a player connected, recording what each
    // is sent: for code that tells GMs something (GameLobby.SendToGmsAsync). The player may
    // not edit the game (the mediator answers CheckPermissionsCommand with false).
    internal static class NoticeLobby
    {
        public static GameLobby Create(Mock<IMediator> mediator, out List<WebSocketCommand> gmSent, out List<WebSocketCommand> playerSent)
        {
            mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var lobby = TestLobby.Create(mediator.Object);
            gmSent = new List<WebSocketCommand>();
            playerSent = new List<WebSocketCommand>();
            lobby.ConnectedPlayers[new PlayerDTO { Id = System.Guid.NewGuid(), Name = "GM", IsOwner = true }] =
                new List<IPlayerConnection> { Connection(gmSent) };
            lobby.ConnectedPlayers[new PlayerDTO { Id = System.Guid.NewGuid(), Name = "Player", IsOwner = false }] =
                new List<IPlayerConnection> { Connection(playerSent) };
            return lobby;
        }

        public static GameLobby Create(IMediator mediator, out List<WebSocketCommand> gmSent, out List<WebSocketCommand> playerSent)
            => Create(Mock.Get(mediator), out gmSent, out playerSent);

        private static IPlayerConnection Connection(List<WebSocketCommand> sent)
        {
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m => { if (m is WebSocketCommand c) lock (sent) sent.Add(c); })
                .ReturnsAsync(true);
            return connection.Object;
        }
    }
}
