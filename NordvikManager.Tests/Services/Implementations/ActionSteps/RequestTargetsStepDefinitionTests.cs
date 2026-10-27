using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    // RequestTargets: the player picks tokens on their map, up to Max of them and only among
    // the Candidates when the action gives some. Cancelling stops the action unless it asks
    // to carry on with nothing picked.
    public class RequestTargetsStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby;
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Wizard" };
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>> _inputs = new();
        private readonly List<JObject> _asked = new();
        private JToken _answer;

        private static JObject Token(string id, string name) => new() { ["id"] = id, ["name"] = name, ["x"] = 1, ["y"] = 2, ["cardId"] = null };

        public RequestTargetsStepDefinitionTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JObject> { Token("a", "Goblin"), Token("b", "Rat"), Token("c", "Orc") });

            var service = new Mock<IActionProcessingService>();
            service.Setup(s => s.InputHandler).Returns(_inputs);
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(service.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };

            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m =>
                {
                    if (m is not WebSocketCommand { Command: "request_targets" } cmd) return;
                    _asked.Add((JObject)cmd.Data);
                    if (_answer != null && _inputs.TryRemove(cmd.InputToken!.Value, out var tcs))
                        tcs.TrySetResult(new WebSocketCommand { Data = _answer });
                })
                .ReturnsAsync(true);
            _lobby.ConnectedPlayers[_player] = new List<IPlayerConnection> { connection.Object };
        }

        private async Task<Dictionary<string, object>> Run(object data)
        {
            var variables = new Dictionary<string, object> { ["Player"] = _player };
            await new RequestTargetsStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "RequestTargets", Data = JObject.FromObject(data) });
            return variables;
        }

        [Fact]
        public async Task AsksTheTriggeringPlayer_AndStoresThePickedTokens()
        {
            _answer = new JArray("b", "a");

            var vars = await Run(new { Max = "3", Message = "Bless whom?", Output = "targets" });

            Assert.Equal(new[] { "b", "a" }, ((JArray)vars["targets"]).Select(t => (string)t["id"]));
            Assert.Equal("Rat", (string)((JArray)vars["targets"])[0]["name"]);
            var asked = Assert.Single(_asked);
            Assert.Equal(3, (int)asked["max"]);
            Assert.Equal("Bless whom?", (string)asked["message"]);
        }

        [Fact]
        public async Task SendsTheCandidates_AndDropsAnythingElse()
        {
            _answer = new JArray("a", "c");

            var vars = await Run(new { Max = "3", Candidates = "[{\"id\":\"a\"},{\"id\":\"b\"}]", Output = "targets" });

            Assert.Equal(new[] { "a", "b" }, _asked[0]["candidates"]!.Select(x => (string)x));
            Assert.Equal(new[] { "a" }, ((JArray)vars["targets"]).Select(t => (string)t["id"]));
        }

        [Fact]
        public async Task NoMoreThanMax()
        {
            _answer = new JArray("a", "b", "c");

            var vars = await Run(new { Max = "2", Output = "targets" });

            Assert.Equal(2, ((JArray)vars["targets"]).Count);
        }

        [Fact]
        public async Task Cancelling_StopsTheAction()
        {
            _answer = new JObject { ["cancelled"] = true };

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Output = "targets" }));
        }

        [Fact]
        public async Task Cancelling_WithStopIfCancelledOff_GivesAnEmptyList()
        {
            _answer = new JObject { ["cancelled"] = true };

            var vars = await Run(new { StopIfCancelled = false, Output = "targets" });

            Assert.Empty((JArray)vars["targets"]);
        }

        [Fact]
        public async Task FewerThanMin_CountsAsCancelled()
        {
            _answer = new JArray("a");

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Min = "2", Max = "3", Output = "targets" }));
        }

        [Fact]
        public async Task PicksTokens_ByDefault()
        {
            _answer = new JArray("a");

            await Run(new { Output = "targets" });

            Assert.Equal("token", (string)_asked[0]["target"]);
            _mediator.Verify(m => m.Send(It.Is<FindCommand>(c => c.What == "tokens"), It.IsAny<CancellationToken>()));
        }

        // Target element: any element on the map (a shape, a picture, a token), in the same
        // shape as picked tokens.
        [Fact]
        public async Task TargetElement_PicksAnyElement_InTheSameShape()
        {
            var wall = new JObject { ["id"] = "w", ["name"] = "Wall", ["type"] = "rect", ["isToken"] = false, ["x"] = 5, ["y"] = 6, ["w"] = 10, ["h"] = 20,
                ["cardId"] = null, ["mapId"] = "m", ["object"] = new JObject() };
            _mediator.Setup(m => m.Send(It.Is<FindCommand>(c => c.What == "elements"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JObject> { wall });
            _answer = new JArray("w");

            var vars = await Run(new { Target = "element", Output = "targets" });

            Assert.Equal("element", (string)_asked[0]["target"]);
            var picked = (JObject)Assert.Single((JArray)vars["targets"]);
            Assert.Equal(new[] { "id", "name", "x", "y", "w", "h", "cardId", "mapId" }, picked.Properties().Select(p => p.Name));
            Assert.Equal("Wall", (string)picked["name"]);
        }

        [Fact]
        public async Task TargetToken_DropsAnElementThatIsNotAToken()
        {
            _answer = new JArray("w", "a");

            var vars = await Run(new { Max = "2", Output = "targets" });

            Assert.Equal(new[] { "a" }, ((JArray)vars["targets"]).Select(t => (string)t["id"]));
        }

        [Fact]
        public async Task AnUnknownTarget_IsAnError()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new { Target = "point", Output = "targets" }));
        }

        [Fact]
        public async Task APlayerWhoIsNotConnected_CountsAsCancelled()
        {
            _lobby.ConnectedPlayers.Clear();

            await Assert.ThrowsAsync<ActionExitException>(() => Run(new { Output = "targets" }));
        }
    }
}
