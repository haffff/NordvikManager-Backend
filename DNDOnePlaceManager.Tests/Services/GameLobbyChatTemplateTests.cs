using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.Commands.Player.GetGamePlayers;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Services
{
    // Server-made chat (roll cards from action steps, Rolls.Finish Html messages) goes
    // through GameLobby.HandleCommand like a player's message. Players must receive it
    // marked isTemplate; a player claiming the flag in their own JSON must not.
    public class GameLobbyChatTemplateTests
    {
        // Serializes like WebRTCPlayerConnection, so the test sees the wire payload.
        private sealed class WireConnection : IPlayerConnection
        {
            public List<JObject> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(JObject.Parse(JsonConvert.SerializeObject(message)));
                return Task.FromResult(true);
            }
        }

        private readonly PlayerDTO _gm = new() { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
        private readonly PlayerDTO _alice = new() { Id = Guid.NewGuid(), Name = "Alice" };
        private readonly WireConnection _gmConnection = new();
        private readonly WireConnection _aliceConnection = new();
        private readonly GameLobby _lobby;

        public GameLobbyChatTemplateTests()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AddMessageCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CommandResponse.Ok, 0L));
            mediator.Setup(m => m.Send(It.IsAny<GetGamePlayersCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<PlayerDTO> { _gm, _alice });
            var scopes = new ServiceCollection()
                .AddSingleton(mediator.Object)
                .AddSingleton(new Mock<IChatService>().Object)
                .AddSingleton(new Mock<DNDOnePlaceManager.Services.Interfaces.IActionProcessingService>().Object)
                .AddScoped<IWebSocketHandler, ChatHandler>()
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            _lobby.ConnectedPlayers[_gm] = new List<IPlayerConnection> { _gmConnection };
            _lobby.ConnectedPlayers[_alice] = new List<IPlayerConnection> { _aliceConnection };
        }

        private static JObject LastChat(WireConnection connection) =>
            connection.Sent.Last(m => m["command"]?.ToString() == "chat_push");

        [Fact]
        public async Task ServerTemplate_ReachesPlayersMarkedAsATemplate()
        {
            await _lobby.HandleCommand(_lobby.SystemPlayer, new WebSocketCommand
            {
                Command = "chat_push",
                Data = JObject.Parse("{\"type\":\"Roll\",\"title\":\"Attack\"}"),
                FromServer = true,
            });

            foreach (var connection in new[] { _gmConnection, _aliceConnection })
            {
                var message = LastChat(connection);
                Assert.True(message.Value<bool>("isTemplate"));
                Assert.Null(message.Property("fromServer", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public async Task PlayerJson_ClaimingTheFlag_ReachesPlayersAsText()
        {
            await _lobby.HandleCommand(_alice, "{\"command\":\"chat_push\",\"data\":\"{\\\"type\\\":\\\"Roll\\\"}\",\"isTemplate\":true,\"fromServer\":true}");

            Assert.False(LastChat(_gmConnection).Value<bool>("isTemplate"));
        }
    }
}
