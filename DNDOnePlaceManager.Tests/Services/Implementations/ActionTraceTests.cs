using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations
{
    // The action editor's Run button asks for a trace: one message per step (running →
    // done/failed/exited, with variables) plus a "finished" message, sent only to the player
    // who ran it. The important case is a failing sub-action: its exception never reaches
    // the calling If/ExecuteAction step, so the trace must still mark that step as failed.
    public class ActionTraceTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly List<JObject> _sent = new();
        private readonly List<ActionDto> _actions = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly IActionProcessingService _service;

        public ActionTraceTests()
        {
            var steps = new IActionStepDefinition[]
            {
                new SetVariableStepDefinition(), new IfStepDefinition(), new LogStepDefinition(),
                new ExitStepDefinition(), new CalculateStepDefinition(),
            };

            var games = new Mock<DbSet<GameModel>>();
            var db = new Mock<IDbContext>();
            db.Setup(d => d.Games).Returns(games.Object);

            _mediator.Setup(m => m.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (CommandResponse.Ok, _actions));
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            IActionProcessingService service = null;
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IEnumerable<IActionStepDefinition>))).Returns(steps);
            provider.Setup(p => p.GetService(typeof(IDbContext))).Returns(db.Object);
            provider.Setup(p => p.GetService(typeof(IMediator))).Returns(_mediator.Object);
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(() => service);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            // ActionProcessingService is internal to the API assembly.
            var type = typeof(IActionProcessingService).Assembly
                .GetType("DNDOnePlaceManager.Services.Implementations.ActionProcessingService");
            service = (IActionProcessingService)Activator.CreateInstance(type, scopeFactory.Object);

            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m =>
                {
                    if (m is WebSocketCommand cmd && cmd.Command == "action_trace")
                        lock (_sent) _sent.Add((JObject)cmd.Data);
                })
                .ReturnsAsync(true);

            var lobby = new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
                ConnectedPlayers = new Dictionary<PlayerDTO, List<IPlayerConnection>> { [_player] = new() { connection.Object } },
            };
            _service = lobby.ActionProcessingService;
        }

        private void AddAction(string name, params object[] steps) => _actions.Add(new ActionDto
        {
            Id = Guid.NewGuid(), Prefix = "t", Name = name, IsEnabled = true, Hook = 0,
            Content = JsonConvert.SerializeObject(steps),
        });

        private static object Step(string id, string type, object data) => new { id, Type = type, Data = data };

        private async Task<List<JObject>> Run(string name)
        {
            var trace = new ActionTrace { TraceId = Guid.NewGuid(), Player = _player };
            await _service.ExecActionAsync($"t/{name}", new CommandHookArgs { Player = _player }, null, trace);
            return _sent.Where(m => m["traceId"]?.ToString() == trace.TraceId.ToString()).ToList();
        }

        private static JObject Last(List<JObject> msgs, string stepId) =>
            msgs.Last(m => m["stepId"]?.ToString() == stepId);

        [Fact]
        public async Task CompletedRun_ReportsEachStepWithVariables_ThenFinished()
        {
            AddAction("ok",
                Step("s1", "SetVariable", new { Name = "hp", Value = "12" }),
                Step("s2", "Log", new { Message = "hi" }));

            var msgs = await Run("ok");

            Assert.Equal("done", Last(msgs, "s1")["status"]?.ToString());
            Assert.Equal("12", Last(msgs, "s1")["variables"]?["hp"]?.ToString());
            Assert.Equal("done", Last(msgs, "s2")["status"]?.ToString());
            var finished = msgs.Last();
            Assert.Equal("finished", finished["status"]?.ToString());
            Assert.Equal("Completed", finished["state"]?.ToString());
        }

        [Fact]
        public async Task FailingSubAction_MarksTheCallingStepFailed()
        {
            AddAction("broken", Step("b1", "Calculate", new { Expression = "1 +", OutputName = "x" }));
            AddAction("caller",
                Step("c1", "If", new { Condition = "1 = 1", ActionTrue = "t/broken" }),
                Step("c2", "Log", new { Message = "after" }));

            var msgs = await Run("caller");

            var ifStep = Last(msgs, "c1");
            Assert.Equal("failed", ifStep["status"]?.ToString());
            Assert.Contains("t/broken failed", ifStep["error"]?.ToString());
            // The caller still continues, exactly as it does without tracing.
            Assert.Equal("done", Last(msgs, "c2")["status"]?.ToString());
            Assert.Equal("Completed", msgs.Last()["state"]?.ToString());
            // The sub-action itself isn't traced.
            Assert.DoesNotContain(msgs, m => m["stepId"]?.ToString() == "b1");
        }

        [Fact]
        public async Task MissingSubAction_MarksTheCallingStepFailed()
        {
            AddAction("caller2", Step("c1", "If", new { Condition = "1 = 1", ActionTrue = "t/nope" }));

            var msgs = await Run("caller2");

            Assert.Equal("failed", Last(msgs, "c1")["status"]?.ToString());
            Assert.Contains("'t/nope' not found", Last(msgs, "c1")["error"]?.ToString());
        }

        [Fact]
        public async Task Exit_ReportsExitedStepAndExitedRun()
        {
            AddAction("stops",
                Step("e1", "Exit", new { Message = "bye" }),
                Step("e2", "Log", new { Message = "never" }));

            var msgs = await Run("stops");

            Assert.Equal("exited", Last(msgs, "e1")["status"]?.ToString());
            Assert.DoesNotContain(msgs, m => m["stepId"]?.ToString() == "e2");
            Assert.Equal("Exited", msgs.Last()["state"]?.ToString());
            Assert.Equal("bye", msgs.Last()["error"]?.ToString());
        }

        [Fact]
        public async Task FailingStep_ReportsFailedStepAndFaultedRun()
        {
            AddAction("bad", Step("f1", "Calculate", new { Expression = "1 +", OutputName = "x" }));

            var msgs = await Run("bad");

            Assert.Equal("failed", Last(msgs, "f1")["status"]?.ToString());
            Assert.Equal("Faulted", msgs.Last()["state"]?.ToString());
        }

        [Fact]
        public async Task PlayerWithoutEditPermission_GetsNoTrace()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            AddAction("quiet", Step("q1", "Log", new { Message = "x" }));

            var msgs = await Run("quiet");

            Assert.Empty(msgs);
        }
    }
}
