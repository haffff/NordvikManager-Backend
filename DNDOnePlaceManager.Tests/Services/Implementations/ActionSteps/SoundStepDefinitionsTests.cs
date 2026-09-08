using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the Play Sound / Stop Sound action steps, added so an action can trigger a
    // soundboard one-shot (e.g. "on crit, play horn") for everyone or a single player,
    // emitting the same sound_play / sound_stop event the soundboard panel does.
    public class SoundStepDefinitionsTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlaySoundStepDefinition _play = new();
        private readonly StopSoundStepDefinition _stop = new();

        private static ActionStep MakeStep(string type, string resourceId, string player = null) =>
            new ActionStep
            {
                Type = type,
                Data = JObject.FromObject(new { ResourceId = resourceId, Player = player }),
            };

        // Same GameLobby construction as CreateCardStepDefinitionTests — the constructor
        // eagerly resolves services via the scope factory.
        private static GameLobby MakeLobby()
        {
            var actionProcessingService = new Mock<IActionProcessingService>();

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(actionProcessingService.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        private static Mock<IPlayerConnection> AddPlayer(GameLobby lobby, out PlayerDTO player, string name = "Alice")
        {
            player = new PlayerDTO { Id = Guid.NewGuid(), Name = name };
            var conn = new Mock<IPlayerConnection>();
            conn.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).ReturnsAsync(true);
            lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { conn.Object };
            return conn;
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-guid")]
        [InlineData(null)]
        public async Task PlaySound_InvalidResourceId_Throws(string? resourceId)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _play.Execute(_mediator.Object, new(), MakeLobby(), MakeStep("PlaySound", resourceId)));
        }

        [Fact]
        public async Task PlaySound_NoPlayer_BroadcastsSoundPlayWithResourceId()
        {
            var lobby = MakeLobby();
            var conn = AddPlayer(lobby, out _);
            var resourceId = Guid.NewGuid();

            await _play.Execute(_mediator.Object, new(), lobby, MakeStep("PlaySound", resourceId.ToString()));

            conn.Verify(c => c.SendMessageToPlayer(It.Is<WebSocketCommand>(cmd =>
                cmd.Command == "sound_play" &&
                (Guid)cmd.Data["resourceId"] == resourceId)), Times.Once);
        }

        [Fact]
        public async Task PlaySound_TargetedPlayer_OnlyThatPlayerReceivesIt()
        {
            var lobby = MakeLobby();
            var aliceConn = AddPlayer(lobby, out var alice, "Alice");
            var bobConn = AddPlayer(lobby, out _, "Bob");

            await _play.Execute(_mediator.Object, new(), lobby,
                MakeStep("PlaySound", Guid.NewGuid().ToString(), player: "Alice"));

            aliceConn.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Once);
            bobConn.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task PlaySound_UnknownPlayer_Throws()
        {
            var lobby = MakeLobby();
            AddPlayer(lobby, out _, "Alice");

            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _play.Execute(_mediator.Object, new(), lobby,
                    MakeStep("PlaySound", Guid.NewGuid().ToString(), player: "Nobody")));
        }

        [Fact]
        public async Task StopSound_NoPlayer_BroadcastsSoundStopWithResourceId()
        {
            var lobby = MakeLobby();
            var conn = AddPlayer(lobby, out _);
            var resourceId = Guid.NewGuid();

            await _stop.Execute(_mediator.Object, new(), lobby, MakeStep("StopSound", resourceId.ToString()));

            conn.Verify(c => c.SendMessageToPlayer(It.Is<WebSocketCommand>(cmd =>
                cmd.Command == "sound_stop" &&
                (Guid)cmd.Data["resourceId"] == resourceId)), Times.Once);
        }

        [Fact]
        public async Task StopSound_InvalidResourceId_Throws()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _stop.Execute(_mediator.Object, new(), MakeLobby(), MakeStep("StopSound", "nope")));
        }

        [Fact]
        public void Definitions_ExposeAudioCategoryMetadata()
        {
            Assert.Equal("PlaySound", _play.Value);
            Assert.Equal("StopSound", _stop.Value);
            Assert.Equal("Audio", _play.Category);
            Assert.Equal("Audio", _stop.Category);
            Assert.Equal(typeof(DNDOnePlaceManager.Services.Implementations.ActionBody.Data.PlaySoundStepData), _play.DataType);
            Assert.Equal(typeof(DNDOnePlaceManager.Services.Implementations.ActionBody.Data.StopSoundStepData), _stop.DataType);
        }

        [Fact]
        public void DataFields_DeclareEditorPickerTypes()
        {
            static string UiType(Type t, string prop) =>
                t.GetProperty(prop)!.GetCustomAttribute<UITypeAttribute>()?.Type;

            Assert.Equal("audioresourceid", UiType(_play.DataType, "ResourceId"));
            Assert.Equal("audioresourceid", UiType(_stop.DataType, "ResourceId"));
            Assert.Equal("playerid", UiType(_play.DataType, "Player"));
            Assert.Equal("playerid", UiType(_stop.DataType, "Player"));
        }
    }
}
