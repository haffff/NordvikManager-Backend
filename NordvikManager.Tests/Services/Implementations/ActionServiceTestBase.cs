using NordvikManager.Application.Commands.Actions.GetActions;
using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
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

namespace NordvikManager.Tests.Services.Implementations
{
    // An ActionProcessingService over mocks: actions are given as step lists, the GM's
    // browser records the trace messages it is sent.
    public abstract class ActionServiceTestBase
    {
        protected readonly Mock<IMediator> _mediator = new();
        protected readonly List<JObject> _sent = new();
        protected readonly List<ActionDto> _actions = new();
        protected readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        protected readonly IActionProcessingService _service;

        protected ActionServiceTestBase()
        {
            var steps = new IActionStepDefinition[]
            {
                new SetVariableStepDefinition(), new IfStepDefinition(), new ForEachStepDefinition(), new LogStepDefinition(),
                new ExitStepDefinition(), new RunScriptStepDefinition(), new ExecuteActionStepDefinition(),
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
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            // ActionProcessingService is internal to the API assembly.
            var type = typeof(IActionProcessingService).Assembly
                .GetType("NordvikManager.Services.Implementations.ActionProcessingService");
            service = (IActionProcessingService)Activator.CreateInstance(type, scopeFactory.Object);

            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m =>
                {
                    if (m is WebSocketCommand cmd && cmd.Command == "action_trace")
                        lock (_sent) _sent.Add((JObject)cmd.Data);
                })
                .ReturnsAsync(true);

            _lobby = new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
                ConnectedPlayers = new Dictionary<PlayerDTO, List<IPlayerConnection>> { [_player] = new() { connection.Object } },
            };
            _service = _lobby.ActionProcessingService;
        }

        protected readonly GameLobby _lobby;

        protected void AddAction(string name, params object[] steps) => _actions.Add(new ActionDto
        {
            Id = Guid.NewGuid(), Prefix = "t", Name = name, IsEnabled = true, Hook = 0,
            Content = JsonConvert.SerializeObject(steps),
        });

        protected static object Step(string id, string type, object data) => new { id, Type = type, Data = data };

        protected async Task<List<JObject>> Run(string name, Dictionary<string, object> args = null)
        {
            var trace = new ActionTrace { TraceId = Guid.NewGuid(), Player = _player };
            await _service.ExecActionAsync($"t/{name}", new CommandHookArgs { Player = _player }, args, trace);
            return _sent.Where(m => m["traceId"]?.ToString() == trace.TraceId.ToString()).ToList();
        }

        protected static JObject Last(List<JObject> msgs, string stepId) =>
            msgs.Last(m => m["stepId"]?.ToString() == stepId);

    }
}
