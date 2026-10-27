using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
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

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
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
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
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

        private PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Wizard", Color = "#3366ff" };

        private async Task<Dictionary<string, object>> Run(object data)
        {
            var variables = new Dictionary<string, object> { ["mapId"] = _map.ToString(), ["Player"] = _player };
            await new ShowMarkerStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "ShowMarker", Data = JObject.FromObject(data) });
            return variables;
        }

        [Fact]
        public async Task ACircle_ToTheViewsShowingTheMap_SizedInSquares()
        {
            var vars = await Run(new { Shape = "circle", At = "{\"x\": 150, \"y\": 125.5}", Radius = "4", Color = "rgba(255,80,0,0.4)", Output = "marker" });

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
            Assert.Equal("rgba(255,80,0,0.4)", (string)shape["fill"]); // the action's colour wins
        }

        // Without a colour of its own, a marker takes the colour of the player whose action it
        // is — as their measuring does — see-through inside.
        [Fact]
        public async Task WithoutAColour_TakesThePlayersColour()
        {
            await Run(new { Shape = "circle", At = "0,0", Radius = "1" });

            var shape = (JObject)((JArray)Assert.Single(_sent, c => c.Command == "preview_start").Data)[0];
            Assert.Equal("rgba(51,102,255,0.25)", (string)shape["fill"]);
            Assert.Equal("rgb(51,102,255)", (string)shape["stroke"]);
        }

        [Fact]
        public async Task ASeeThroughPlayerColour_IsNoColour()
        {
            _player = new PlayerDTO { Id = Guid.NewGuid(), Name = "Wizard", Color = "rgb(0,0,0,0)" };

            await Run(new { Shape = "circle", At = "0,0", Radius = "1" });

            var shape = (JObject)((JArray)Assert.Single(_sent, c => c.Command == "preview_start").Data)[0];
            Assert.Equal("rgba(255,120,0,0.35)", (string)shape["fill"]);
        }

        [Fact]
        public async Task APlayerWithoutAUsableColour_GetsTheDefault()
        {
            _player = new PlayerDTO { Id = Guid.NewGuid(), Name = "Wizard", Color = "teal-ish" };

            await Run(new { Shape = "circle", At = "0,0", Radius = "1" });

            var shape = (JObject)((JArray)Assert.Single(_sent, c => c.Command == "preview_start").Data)[0];
            Assert.Equal("rgba(255,120,0,0.35)", (string)shape["fill"]);
        }

        // A cone or line is drawn as the area Find uses (AreaShape): the same corners.
        [Fact]
        public async Task ACone_FromAPointTowardAnother_IsTheTriangleFindUses()
        {
            await Run(new { Shape = "cone", At = "100,100", Toward = "{\"x\": 200, \"y\": 100}", Radius = "3", Angle = "90" });

            var shape = (JObject)((JArray)Assert.Single(_sent, c => c.Command == "preview_start").Data)[0];
            Assert.Equal("polygon", (string)shape["type"]);
            var points = ((JArray)shape["points"]).Select(p => ((double)p["x"], (double)p["y"])).ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal((100.0, 100.0), points[0]);
            Assert.Equal(100 + 150 * Math.Cos(Math.PI / 4), points[1].Item1, 6);
            Assert.Equal(100 - 150 * Math.Sin(Math.PI / 4), points[1].Item2, 6);
        }

        [Fact]
        public async Task ALine_IsFourCornersWideAsItsWidth()
        {
            await Run(new { Shape = "line", At = "0,0", Toward = "10,0", Radius = "4", Width = "1" });

            var shape = (JObject)((JArray)Assert.Single(_sent, c => c.Command == "preview_start").Data)[0];
            var points = ((JArray)shape["points"]).Select(p => ((double)p["x"], (double)p["y"])).ToList();
            Assert.Equal(new[] { (0.0, 25.0), (200.0, 25.0), (200.0, -25.0), (0.0, -25.0) }, points.Select(p => (Math.Round(p.Item1, 6), Math.Round(p.Item2, 6))));
        }

        [Fact]
        public async Task ALineOrCone_NeedsToward()
        {
            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.ActionProcessException>(() =>
                Run(new { Shape = "cone", At = "0,0", Radius = "3" }));
        }

        [Fact]
        public async Task At_IsRequired()
        {
            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.ActionProcessException>(() =>
                Run(new { Shape = "circle", Radius = "3" }));
        }

        [Fact]
        public async Task GoesAwayAfterItsDuration()
        {
            await Run(new { Shape = "rect", At = "0,0", Width = "2", Height = "1", Duration = "0.05" });

            // The marker goes away on a background timer: wait for it rather than a fixed time,
            // which a busy CI runner can overrun.
            WebSocketCommand[] Sent() { lock (_sent) return _sent.ToArray(); }
            for (var i = 0; i < 100 && !Sent().Any(c => c.Command == "preview_end"); i++)
                await Task.Delay(50);
            var end = Assert.Single(Sent(), c => c.Command == "preview_end");
            Assert.Equal(_view.ToString(), end.BattleMapId);
        }

        [Fact]
        public async Task UntilActionEnds_GoesAwayWithTheAction()
        {
            await Run(new { Shape = "text", At = "0,0", Text = "Boom", UntilActionEnds = true });

            Assert.DoesNotContain(_sent, c => c.Command == "preview_end");
            await _atEnd();
            Assert.Single(_sent, c => c.Command == "preview_end");
        }
    }
}
