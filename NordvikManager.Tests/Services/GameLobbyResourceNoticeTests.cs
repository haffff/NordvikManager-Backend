using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Application.Commands.Security.GetEffectivePermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
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
    // A resource can be read by anyone in the game who knows its id or key, so a new
    // resource's id must reach only who made it and the GMs (who list every resource anyway),
    // as GetResources lists them. It used to go to every connected player: a GM's PDF uploaded
    // from a card, or a card's private file, was then readable by all of them.
    public class GameLobbyResourceNoticeTests
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
        private readonly PlayerDTO _coGm = new() { Id = Guid.NewGuid(), Name = "Co-GM" };
        private readonly PlayerDTO _ana = new() { Id = Guid.NewGuid(), Name = "Ana" };
        private readonly PlayerDTO _bo = new() { Id = Guid.NewGuid(), Name = "Bo" };
        private readonly Dictionary<PlayerDTO, RecordingConnection> _connections = new();
        private readonly GameLobby _lobby;

        public GameLobbyResourceNoticeTests()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IRequest<bool> c, CancellationToken _) => ((CheckPermissionsCommand)c).Player.Id == _coGm.Id);
            // a new resource has no permission rows of its own
            mediator.Setup(m => m.Send(It.IsAny<GetEffectivePermissionsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Dictionary<Guid, Permission>());
            var scopes = new ServiceCollection()
                .AddSingleton(mediator.Object)
                .AddSingleton(new Mock<NordvikManager.Services.Interfaces.IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            foreach (var player in new[] { _gm, _coGm, _ana, _bo })
            {
                var connection = new RecordingConnection();
                _connections[player] = connection;
                _lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
            }
        }

        private static WebSocketCommand Notice(string command) => new()
        {
            Command = command,
            Result = WebSocketCommandNames.ResultOk,
            Data = JObject.FromObject(new { id = Guid.NewGuid(), name = "Old Mill.pdf", mimeType = "application/pdf" }),
        };

        private int Got(PlayerDTO player, string command) => _connections[player].Sent.Count(c => c.Command == command);

        [Fact]
        public async Task ResourceAdd_ReachesOnlyItsMakerAndTheGms_WhenTheGmMakesOne()
        {
            await _lobby.HandlePostCommand(_gm, Notice(WebSocketCommandNames.ResourceAdd));

            Assert.Equal(1, Got(_gm, WebSocketCommandNames.ResourceAdd));
            Assert.Equal(1, Got(_coGm, WebSocketCommandNames.ResourceAdd));
            Assert.Equal(0, Got(_ana, WebSocketCommandNames.ResourceAdd));
            Assert.Equal(0, Got(_bo, WebSocketCommandNames.ResourceAdd));
        }

        [Fact]
        public async Task ResourceAdd_ReachesThePlayerWhoMadeIt_AndNotTheOtherPlayers()
        {
            await _lobby.HandlePostCommand(_ana, Notice(WebSocketCommandNames.ResourceAdd));

            Assert.Equal(1, Got(_ana, WebSocketCommandNames.ResourceAdd));
            Assert.Equal(1, Got(_gm, WebSocketCommandNames.ResourceAdd));
            Assert.Equal(0, Got(_bo, WebSocketCommandNames.ResourceAdd));
        }

        // Players' themes reload on resource_update (GameStylesheets): it stays for everyone.
        [Fact]
        public async Task ResourceUpdate_StillReachesEveryone()
        {
            await _lobby.HandlePostCommand(_gm, Notice(WebSocketCommandNames.ResourceUpdate));

            Assert.Equal(1, Got(_ana, WebSocketCommandNames.ResourceUpdate));
            Assert.Equal(1, Got(_bo, WebSocketCommandNames.ResourceUpdate));
        }
    }
}
