using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // RequestPoint: the player points at a place on their map, with a marker (the area of a
    // spell) following the cursor, snapped to the grid and kept within range when asked.
    public class RequestPointStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby;
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Wizard" };
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>> _inputs = new();
        private readonly List<JObject> _asked = new();
        private readonly Guid _mapId = Guid.NewGuid();
        private JToken _answer;

        public RequestPointStepDefinitionTests()
        {
            _mediator.Setup(m => m.Send(It.Is<FindCommand>(c => c.What == "maps"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JObject> { new() { ["id"] = _mapId.ToString(), ["gridSize"] = 50 } });
            _mediator.Setup(m => m.Send(It.Is<FindCommand>(c => c.What == "tokens"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JObject> { new() { ["id"] = "tok", ["x"] = 100, ["y"] = 100, ["w"] = 50, ["h"] = 50 } });

            var service = new Mock<IActionProcessingService>();
            service.Setup(s => s.InputHandler).Returns(_inputs);
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(service.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };

            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m =>
                {
                    if (m is not WebSocketCommand { Command: "request_point" } cmd) return;
                    _asked.Add((JObject)cmd.Data);
                    if (_answer != null && _inputs.TryRemove(cmd.InputToken!.Value, out var tcs))
                        tcs.TrySetResult(new WebSocketCommand { Data = _answer });
                })
                .ReturnsAsync(true);
            _lobby.ConnectedPlayers[_player] = new List<IPlayerConnection> { connection.Object };
        }

        private JObject At(double x, double y) => new() { ["x"] = x, ["y"] = y, ["mapId"] = _mapId.ToString() };

        private async Task<Dictionary<string, object>> Run(object data)
        {
            var variables = new Dictionary<string, object> { ["Player"] = _player, ["mapId"] = _mapId.ToString() };
            await new RequestPointStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "RequestPoint", Data = JObject.FromObject(data) });
            return variables;
        }

        [Fact]
        public async Task AsksWithTheMarker_AndStoresThePoint()
        {
            _answer = At(120, 80);

            var vars = await Run(new { Message = "Where does it go off?", Shape = "circle", Radius = "4", Output = "point" });

            var point = (JObject)vars["point"];
            Assert.Equal(new[] { 120.0, 80.0 }, new[] { (double)point["x"], (double)point["y"] });
            Assert.Equal(_mapId.ToString(), (string)point["mapId"]);
            var asked = Assert.Single(_asked);
            Assert.Equal("Where does it go off?", (string)asked["message"]);
            Assert.Equal(_mapId.ToString(), (string)asked["mapId"]);
            Assert.Equal("circle", (string)asked["marker"]["type"]);
            Assert.Equal(200.0, (double)asked["marker"]["radius"]);
            Assert.True((bool)asked["highlight"]);
            Assert.True((bool)asked["showToOthers"]);
            Assert.Equal("none", (string)asked["snap"]);
        }

        [Fact]
        public async Task ShapeNone_AsksForAPointWithoutAMarker()
        {
            _answer = At(1, 2);

            await Run(new { Shape = "none", Output = "point" });

            Assert.Equal(JTokenType.Null, _asked[0]["marker"]!.Type);
        }

        [Theory]
        [InlineData("center")]
        [InlineData("corner")]
        public async Task SendsHowToSnap(string snap)
        {
            _answer = At(1, 2);

            await Run(new { SnapToGrid = snap, Output = "point" });

            Assert.Equal(snap, (string)_asked[0]["snap"]);
        }

        [Fact]
        public async Task AnUnknownSnap_IsAnError()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new { SnapToGrid = "hex", Output = "point" }));
        }

        [Fact]
        public async Task RangeFromAPoint_IsSent_AndAPointOutsideCountsAsCancelled()
        {
            _answer = At(300, 0);

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { From = "{\"x\":0,\"y\":0}", MaxDistance = "2", Output = "point" }));

            Assert.Equal(new[] { 0.0, 0.0, 100.0 }, new[] { (double)_asked[0]["range"]["x"], (double)_asked[0]["range"]["y"], (double)_asked[0]["range"]["radius"] });
        }

        // From a token, the range counts from its edge, like Find's Near/Radius.
        [Fact]
        public async Task RangeFromAToken_CountsFromItsEdge()
        {
            _answer = At(100, 220);

            var vars = await Run(new { From = "tok", MaxDistance = "2", Output = "point" });

            Assert.Equal(125.0, (double)_asked[0]["range"]["radius"]);
            Assert.Equal(220.0, (double)((JObject)vars["point"])["y"]);
        }

        [Fact]
        public async Task RangeFromAPickedToken_TakesItsId()
        {
            _answer = At(100, 100);

            await Run(new { From = "{\"id\":\"tok\",\"name\":\"Wizard\"}", MaxDistance = "1", Output = "point" });

            Assert.Equal(100.0, (double)_asked[0]["range"]["x"]);
        }

        [Fact]
        public async Task AnUnknownToken_IsAnError()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new { From = "nope", MaxDistance = "1", Output = "point" }));
        }

        [Fact]
        public async Task Cancelling_StopsTheAction()
        {
            _answer = new JObject { ["cancelled"] = true };

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Output = "point" }));
        }

        [Fact]
        public async Task Cancelling_WithStopIfCancelledOff_LeavesItEmpty()
        {
            _answer = new JObject { ["cancelled"] = true };

            var vars = await Run(new { StopIfCancelled = false, Output = "point" });

            Assert.Equal(JTokenType.Null, ((JToken)vars["point"]).Type);
        }

        [Fact]
        public async Task AnAnswerForAnotherMap_CountsAsCancelled()
        {
            _answer = new JObject { ["x"] = 1, ["y"] = 2, ["mapId"] = Guid.NewGuid().ToString() };

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Output = "point" }));
        }

        [Fact]
        public async Task AnAnswerThatIsNotAPoint_CountsAsCancelled()
        {
            _answer = new JObject { ["x"] = "far", ["y"] = 2, ["mapId"] = _mapId.ToString() };

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Output = "point" }));
        }

        [Fact]
        public async Task OutputIsRequired()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new { Shape = "circle" }));
        }
    }
}
