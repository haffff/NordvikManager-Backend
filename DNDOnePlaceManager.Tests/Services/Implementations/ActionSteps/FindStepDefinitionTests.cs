using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Find: "the tokens" means the tokens of the map the player is looking at, unless the
    // action says otherwise; Near takes a point or a token; First gives one result.
    public class FindStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly List<FindCommand> _finds = new();
        private readonly Guid _onlyViewMap = Guid.NewGuid();
        private List<BattleMapDto> _views;
        private readonly GameLobby _lobby;

        public FindStepDefinitionTests()
        {
            _views = new List<BattleMapDto> { new() { Id = Guid.NewGuid(), Name = "Table", MapId = _onlyViewMap } };
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<List<JObject>>, CancellationToken>((c, _) => _finds.Add((FindCommand)c))
                .ReturnsAsync(() => new List<JObject> { new JObject { ["id"] = "a" }, new JObject { ["id"] = "b" } });
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _views);

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
        }

        private async Task<Dictionary<string, object>> Run(object data, Dictionary<string, object> variables = null)
        {
            variables ??= new Dictionary<string, object>();
            await new FindStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "Find", Data = JObject.FromObject(data) });
            return variables;
        }

        // Near a card's id (%cardId%) finds nothing, as the card isn't on the map: say so instead.
        [Fact]
        public async Task NearACard_SaysToUseItsToken()
        {
            var card = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<List<JObject>> c, CancellationToken _) => ((FindCommand)c) is { What: "cards", Id: var id } && id == card
                    ? new List<JObject> { new() { ["id"] = card.ToString(), ["name"] = "Wizard" } }
                    : new List<JObject>());

            var e = await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new { What = "tokens", Near = card.ToString(), Radius = "2", Output = "t" }, new Dictionary<string, object> { ["mapId"] = Guid.NewGuid().ToString() }));

            Assert.Contains("is a card", e.Message);
        }

        [Fact]
        public async Task NearAToken_FindingNothing_IsNoError()
        {
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<JObject>());

            var vars = await Run(new { What = "tokens", Near = Guid.NewGuid().ToString(), Radius = "2", Output = "t" }, new Dictionary<string, object> { ["mapId"] = Guid.NewGuid().ToString() });

            Assert.Empty((JArray)vars["t"]);
        }

        [Fact]
        public async Task Tokens_DefaultToTheMapThePlayerIsLookingAt()
        {
            var activeMap = Guid.NewGuid();

            await Run(new { What = "tokens", Output = "t" }, new Dictionary<string, object> { ["mapId"] = activeMap.ToString() });

            Assert.Equal(activeMap, _finds[0].MapId);
            Assert.Equal("tokens", _finds[0].What);
        }

        [Fact]
        public async Task Tokens_WithNoActiveMap_UseTheBattleMapViewsMap()
        {
            await Run(new { What = "tokens", Output = "t" });

            Assert.Equal(_onlyViewMap, _finds[0].MapId);
        }

        [Fact]
        public async Task Tokens_AllMaps_SearchesEveryMap()
        {
            await Run(new { What = "tokens", AllMaps = true, Output = "t" }, new Dictionary<string, object> { ["mapId"] = Guid.NewGuid().ToString() });

            Assert.Null(_finds[0].MapId);
        }

        [Fact]
        public async Task Near_TakesAPointVariableOrAToken()
        {
            await Run(new { What = "tokens", Near = "{\"x\": 150, \"y\": 125.5}", Radius = "4", Output = "t" });
            Assert.Equal(150, _finds[0].NearX);
            Assert.Equal(125.5, _finds[0].NearY);
            Assert.Equal(4, _finds[0].Radius);

            var token = Guid.NewGuid();
            await Run(new { What = "tokens", Near = token.ToString(), Radius = "2", Output = "t" });
            Assert.Equal(token, _finds[1].NearElementId);

            await Run(new { What = "tokens", Near = "10, 20", Radius = "1", Output = "t" });
            Assert.Equal(10, _finds[2].NearX);
            Assert.Equal(20, _finds[2].NearY);
        }

        // A token from Find or Request Targets (%target%) is a place too: its id is taken.
        [Fact]
        public async Task Near_TakesATokenObject()
        {
            var token = Guid.NewGuid();

            await Run(new { What = "tokens", Near = $"{{\"id\":\"{token}\",\"name\":\"Wizard\",\"x\":1,\"y\":2}}", Radius = "2", Output = "t" });

            Assert.Equal(token, _finds[0].NearElementId);
        }

        // Request Targets with Max 1 gives a list of one: that token is the place.
        [Fact]
        public async Task Near_TakesAListOfOneToken()
        {
            var token = Guid.NewGuid();

            await Run(new { What = "tokens", Near = $"[{{\"id\":\"{token}\",\"name\":\"Wizard\"}}]", Radius = "2", Output = "t" });

            Assert.Equal(token, _finds[0].NearElementId);
        }

        // The same area arguments as the markers: a cone from the caster toward a point or a token.
        [Fact]
        public async Task ACone_TowardAPointOrAToken()
        {
            var caster = Guid.NewGuid();
            var target = Guid.NewGuid();

            await Run(new { What = "tokens", Near = caster.ToString(), Shape = "cone", Radius = "3", Angle = "60", Toward = "{\"x\": 300, \"y\": 100}", Output = "t" });
            await Run(new { What = "tokens", Near = caster.ToString(), Shape = "Line", Radius = "6", Width = "2", Toward = target.ToString(), Output = "t" });

            Assert.Equal(("cone", 3.0, 60.0, 300.0, 100.0), (_finds[0].Shape, _finds[0].Radius!.Value, _finds[0].Angle!.Value, _finds[0].TowardX!.Value, _finds[0].TowardY!.Value));
            Assert.Equal(("line", 2.0, target), (_finds[1].Shape, _finds[1].Width!.Value, _finds[1].TowardElementId!.Value));
        }

        [Fact]
        public async Task ALineOrCone_NeedsToward()
        {
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ActionProcessException>(() =>
                Run(new { What = "tokens", Near = "1,2", Shape = "cone", Radius = "3", Output = "t" }));
        }

        [Fact]
        public async Task AnAreaShape_NeedsNear()
        {
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ActionProcessException>(() =>
                Run(new { What = "tokens", Shape = "rect", Width = "3", Height = "3", Output = "t" }));
        }

        [Fact]
        public async Task AnUnknownShape_IsAnError()
        {
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ActionProcessException>(() =>
                Run(new { What = "tokens", Near = "1,2", Shape = "hex", Radius = "3", Output = "t" }));
        }

        [Fact]
        public async Task Output_IsTheList_OrWithFirstOneItem()
        {
            var all = await Run(new { What = "cards", Output = "cards" });
            Assert.Equal(2, ((JArray)all["cards"]).Count);

            var first = await Run(new { What = "cards", First = true, Output = "card" });
            Assert.Equal("a", (string)((JObject)first["card"])["id"]);
        }

        [Fact]
        public async Task First_WithNothingFound_IsEmpty()
        {
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<JObject>());

            var vars = await Run(new { What = "cards", First = true, Output = "card" });

            Assert.Null(vars["card"]);
        }

        [Fact]
        public async Task BattleMaps_First_IsThePlayersView_ThenTheOnlyOrFirstOne()
        {
            var second = new BattleMapDto { Id = Guid.NewGuid(), Name = "Side", MapId = Guid.NewGuid() };
            _views = new List<BattleMapDto> { _views[0], second };

            var playersView = await Run(new { What = "battleMaps", First = true, Output = "bm" },
                new Dictionary<string, object> { ["battleMapId"] = second.Id.ToString() });
            Assert.Equal(second.Id, _finds[0].Id);

            await Run(new { What = "battleMaps", First = true, Output = "bm" });
            Assert.Equal(_views[0].Id, _finds[1].Id);
        }

        [Fact]
        public async Task UnknownKind_IsAnActionError()
        {
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ActionProcessException>(() => Run(new { What = "", Output = "x" }));
        }
    }
}
