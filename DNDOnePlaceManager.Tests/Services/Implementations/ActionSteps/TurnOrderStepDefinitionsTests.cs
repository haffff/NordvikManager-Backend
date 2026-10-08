using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Turn order steps change the order as System, then tell everyone (and the hooks)
    // exactly as a player's command would.
    public class TurnOrderStepDefinitionsTests
    {
        private sealed class RecordingConnection : IPlayerConnection
        {
            public List<WebSocketCommand> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message) { if (message is WebSocketCommand c) Sent.Add(c); return Task.FromResult(true); }
        }

        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby;
        private readonly RecordingConnection _everyone = new();
        private readonly Guid _mapId = Guid.NewGuid();
        private readonly Guid _added = Guid.NewGuid();
        private readonly List<TurnOrderCommand> _sent = new();

        public TurnOrderStepDefinitionsTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<TurnOrderCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, TurnOrderNotice?)>, CancellationToken>((c, _) => _sent.Add((TurnOrderCommand)c))
                     .ReturnsAsync(() => (CommandResponse.Ok, new TurnOrderNotice { MapId = _mapId, Round = 1, TurnChanged = true, AddedEntryIds = new() { _added } }));
            var scopes = new ServiceCollection()
                .AddSingleton(_mediator.Object)
                .AddSingleton(new Mock<IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            _lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" }] = new List<IPlayerConnection> { _everyone };
        }

        private async Task<Dictionary<string, object>> Run(IActionStepDefinition step, object data)
        {
            var variables = new Dictionary<string, object>();
            await step.Execute(_mediator.Object, variables, _lobby, new ActionStep { Type = step.Value, Data = JObject.FromObject(data) });
            return variables;
        }

        private TurnOrderCommand Sent => Assert.Single(_sent);

        [Fact]
        public async Task NextTurn_AdvancesAsSystem_AndTellsEveryone()
        {
            await Run(new NextTurnStepDefinition(), new { MapId = _mapId.ToString() });

            Assert.Equal((TurnOrderOperation.Advance, 1), (Sent.Operation, Sent.Direction));
            Assert.Equal((_lobby.GameId, _mapId), (Sent.GameId, Sent.MapId));
            Assert.Same(_lobby.SystemPlayer, Sent.Player);
            var told = Assert.Single(_everyone.Sent);
            Assert.Equal(WebSocketCommandNames.TurnOrderAdvance, told.Command);
            Assert.True(told.Data["turnChanged"]!.Value<bool>());
        }

        [Fact]
        public async Task PreviousTurn_StepsBack()
        {
            await Run(new PreviousTurnStepDefinition(), new { MapId = _mapId.ToString() });

            Assert.Equal((TurnOrderOperation.Advance, -1), (Sent.Operation, Sent.Direction));
        }

        [Fact]
        public async Task AddToTurnOrder_AddsAnEntry_AndOutputsItsId()
        {
            var token = Guid.NewGuid();

            var variables = await Run(new AddToTurnOrderStepDefinition(),
                new { MapId = _mapId.ToString(), ElementId = token.ToString(), Initiative = "17", Hidden = "true", Output = "entry" });

            var entry = Assert.Single(Sent.Entries!);
            Assert.Equal((token, 17.0, true), (entry.ElementId!.Value, entry.Initiative!.Value, entry.Hidden!.Value));
            Assert.Equal(_added.ToString(), variables["entry"]);
        }

        [Fact]
        public async Task SetInitiative_ForAToken_CanSortAfterwards()
        {
            var token = Guid.NewGuid();

            await Run(new SetInitiativeStepDefinition(), new { MapId = _mapId.ToString(), ElementId = token.ToString(), Initiative = "12.5", Sort = "true" });

            Assert.Equal((TurnOrderOperation.Update, (Guid?)token, 12.5, true), (Sent.Operation, Sent.ElementId, Sent.Initiative!.Value, Sent.SortAfter));
        }

        [Fact]
        public async Task GetTurnOrder_OutputsTheState()
        {
            var state = new TurnOrderDto { MapId = _mapId, Round = 3 };
            _mediator.Setup(m => m.Send(It.IsAny<GetTurnOrderCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);

            var variables = await Run(new GetTurnOrderStepDefinition(), new { MapId = _mapId.ToString(), Output = "order" });

            Assert.Equal(3, JObject.FromObject(variables["order"])["Round"]!.Value<int>());
        }

        [Fact]
        public async Task NoMapId_UsesTheGamesOnlyBattleMap()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<BattleMapDto> { new() { Id = Guid.NewGuid(), MapId = _mapId } });

            await Run(new SortTurnOrderStepDefinition(), new { MapId = "" });

            Assert.Equal((TurnOrderOperation.Sort, _mapId), (Sent.Operation, Sent.MapId));
        }

        [Fact]
        public async Task InvalidNumbers_AreReported()
        {
            var ex = await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new AddToTurnOrderStepDefinition(), new { MapId = _mapId.ToString(), Name = "Lair", Initiative = "fast" }));
            Assert.Contains("'Initiative'", ex.Message);
        }
    }
}
