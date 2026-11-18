using MediatR;
using Newtonsoft.Json.Linq;
using NordvikManager.Enums;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    // Stands in for a step that changes something and so fires a hook (SetProperty and the like).
    public class TestFireCommandStep : IActionStepDefinition
    {
        public const string Type = "TestFireCommand";
        public string Name => Type;
        public string Value => Type;
        public IReadOnlyList<string> Keywords => Array.Empty<string>();
        public string Category => "Test";
        public string Description => "Test only.";
        public string? Summary => null;
        public Type DataType => typeof(object);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step) =>
            gameLobby.HandlePostCommand(gameLobby.ConnectedPlayers.Keys.First(), new WebSocketCommand
            {
                Command = WebSocketCommandNames.PropertyUpdate,
                Result = WebSocketCommandNames.ResultOk,
                Data = new JObject(),
            });
    }

    // An action calling itself, or setting off the hook it listens to, used to run until
    // the server fell over, and every game on it with the server.
    public class ActionLoopGuardTests : ActionServiceTestBase
    {
        private int Count(string message) => _lobby.EventLog.GetEntries().Count(e => e.Message.Contains(message));

        [Fact]
        public async Task AnActionCallingItself_IsStopped_AtTheDepthLimit()
        {
            AddAction("a", Step("l", "Log", new { Message = "tick" }), Step("s", "ExecuteAction", new { Value = "t/a" }));

            await Run("a");

            Assert.Equal(ActionCascade.DefaultMaxDepth, Count("tick"));
            Assert.Equal(1, Count("stopped"));
        }

        [Fact]
        public async Task ManySubActionsInARow_AreNotALoop()
        {
            AddAction("leaf", Step("l", "Log", new { Message = "leaf" }));
            AddAction("a", Enumerable.Range(0, ActionCascade.DefaultMaxDepth + 8)
                .Select(i => Step($"s{i}", "ExecuteAction", new { Value = "t/leaf" })).ToArray());

            await Run("a");

            Assert.Equal(ActionCascade.DefaultMaxDepth + 8, Count("leaf"));
            Assert.Equal(0, Count("stopped"));
        }

        [Fact]
        public async Task TwoSeparateRuns_DoNotShareALimit()
        {
            AddAction("a", Step("l", "Log", new { Message = "tick" }), Step("s", "ExecuteAction", new { Value = "t/a" }));

            await Run("a");
            await Run("a");

            Assert.Equal(ActionCascade.DefaultMaxDepth * 2, Count("tick"));
        }

        [Fact]
        public async Task AnActionSettingOffItsOwnHook_IsStopped()
        {
            // Through the real path: a step's command, HandlePostCommand, Task.Run, CommandToHook.
            AddAction("a", Step("l", "Log", new { Message = "tick" }), Step("f", TestFireCommandStep.Type, new { }));
            _actions[0].Hook = Hook.PropertyUpdate;

            await _service.CallHookAsync(Hook.PropertyUpdate, new PlayerHookArgs { Player = _player });

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (Count("stopped") == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(20);
            await Task.Delay(200);

            Assert.Equal(1, Count("stopped"));
            Assert.Equal(ActionCascade.DefaultMaxDepth, Count("tick"));
        }
    }
}
