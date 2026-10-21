using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // ShowMarker: a shape on the map everyone sees for a while (e.g. where a Fireball goes
    // off), sent like a sketch — never saved — to every battle map view showing that map.
    public class ShowMarkerStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IActionProcessingService> _service = new();
        private readonly GameLobby _lobby;
        private readonly List<WebSocketCommand> _sent = new();
        private readonly Guid _map = Guid.NewGuid();
        private readonly Guid _view = Guid.NewGuid(), _otherView = Guid.NewGuid();
        private Func<Task> _atEnd;

        public ShowMarkerStepDefinitionTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BattleMapDto> { new() { Id = _view, MapId = _map }, new() { Id = _otherView, MapId = Guid.NewGuid() } });
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JObject> { new JObject { ["id"] = _map.ToString(), ["gridSize"] = 50 } });
            _service.Setup(s => s.AtActionEnd(It.IsAny<Func<Task>>())).Callback<Func<Task>>(f => _atEnd = f);

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(_service.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };

            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m => { if (m is WebSocketCommand c) lock (_sent) _sent.Add(c); })
                .ReturnsAsync(true);
            _lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Wizard" }] = new List<IPlayerConnection> { connection.Object };
        }

        private async Task<Dictionary<string, object>> Run(object data)
        {
            var variables = new Dictionary<string, object> { ["mapId"] = _map.ToString() };
            await new ShowMarkerStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "ShowMarker", Data = JObject.FromObject(data) });
            return variables;
        }

        [Fact]
        public async Task ACircle_ToTheViewsShowingTheMap_SizedInSquares()
        {
            var vars = await Run(new { Shape = "circle", X = "150", Y = "125.5", Radius = "4", Color = "rgba(255,80,0,0.4)", Output = "marker" });

            var start = Assert.Single(_sent, c => c.Command == "preview_start");
            Assert.Equal(_view.ToString(), start.BattleMapId);
            Assert.Equal(_lobby.SystemPlayer.Id, start.PlayerId);
            var shape = (JObject)((JArray)start.Data)[0];
            Assert.Equal("circle", (string)shape["type"]);
            Assert.Equal(200, (double)shape["radius"]);
            Assert.Equal(125.5, (double)shape["top"]);
            Assert.Equal("center", (string)shape["originX"]);
            Assert.Equal(_lobby.SystemPlayer.Id.ToString(), (string)shape["playerId"]);
            Assert.Equal((string)shape["previewId"], vars["marker"]);
        }

        [Fact]
        public async Task GoesAwayAfterItsDuration()
        {
            await Run(new { Shape = "rect", X = "0", Y = "0", Width = "2", Height = "1", Duration = "0.05" });

            await Task.Delay(400);
            var end = Assert.Single(_sent, c => c.Command == "preview_end");
            Assert.Equal(_view.ToString(), end.BattleMapId);
        }

        [Fact]
        public async Task UntilActionEnds_GoesAwayWithTheAction()
        {
            await Run(new { Shape = "text", X = "0", Y = "0", Text = "Boom", UntilActionEnds = true });

            Assert.DoesNotContain(_sent, c => c.Command == "preview_end");
            await _atEnd();
            Assert.Single(_sent, c => c.Command == "preview_end");
        }
    }
}
