using NordvikManager.Application.Commands.Actions.GetActions;
using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Domain.Enums;
using NordvikManager.Enums;
using NordvikManager.Services.Implementations.HookArgs;
using MediatR;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    // A player starts an action by naming it, in a message any client can send. So who may is
    // decided there: game editors always, anyone else needs Execute on the action. Steps calling
    // other actions and hooks aren't players starting anything, so they aren't checked.
    public class ActionExecutePermissionTests : ActionServiceTestBase
    {
        private void Allow(bool editsGame = false, Guid? execute = null) =>
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<bool> c, CancellationToken _) =>
                {
                    var check = (CheckPermissionsCommand)c;
                    return (editsGame && check.EntityId == _lobby.GameId && check.RequiredPermission == Permission.Edit)
                        || (execute == check.EntityId && check.RequiredPermission == Permission.Execute);
                });

        private Guid Add(string name, bool enabled = true)
        {
            AddAction(name, Step("s1", "Log", new { Message = "hi" }));
            _actions[^1].IsEnabled = enabled;
            return _actions[^1].Id!.Value;
        }

        private bool Started(string name) => _service.RunningActions.Values.Any(e => e.ActionName == name);

        private GetActionsCommand[] Queries() =>
            _mediator.Invocations.Select(i => i.Arguments[0]).OfType<GetActionsCommand>().ToArray();

        [Fact]
        public async Task ExecAction_DoesNotRun_WhenPlayerHasNoExecute()
        {
            Allow();
            Add("secret");

            await Run("secret");

            Assert.False(Started("secret"));
            Assert.Contains(_lobby.EventLog.GetEntries(), e => e.Category == "Permission");
        }

        [Fact]
        public async Task ExecAction_TellsThePlayer_WhenRefused()
        {
            Allow();
            Add("secret");

            await Run("secret");

            var error = Assert.Single(_commands, c => c.Command == "error_permission");
            Assert.True(error.OnlyToSender);
        }

        [Fact]
        public async Task ExecAction_Runs_WhenPlayerHasExecute()
        {
            Allow(execute: Add("open"));

            await Run("open");

            Assert.True(Started("open"));
            Assert.DoesNotContain(_commands, c => c.Command == "error_permission");
        }

        [Fact]
        public async Task ExecAction_IgnoresGmPermission_WhenPlayerHasExecute()
        {
            Allow(execute: Add("open"));
            _actions[^1].GmPermission = Permission.All;

            await Run("open");

            Assert.True(Started("open"));
        }

        [Fact]
        public async Task ExecAction_Runs_WhenPlayerOwnsTheGame()
        {
            Allow();
            Add("secret");
            _player.IsOwner = true;

            await Run("secret");

            Assert.True(Started("secret"));
        }

        [Fact]
        public async Task ExecAction_Runs_WhenPlayerIsACoGm()
        {
            Allow(editsGame: true);
            Add("secret");

            await Run("secret");

            Assert.True(Started("secret"));
        }

        [Fact]
        public async Task ExecAction_DoesNotRun_WhenActionIsDisabledAndPlayerHasExecute()
        {
            Allow(execute: Add("off", enabled: false));

            await Run("off");

            Assert.False(Started("off"));
        }

        [Fact]
        public async Task ExecAction_Runs_WhenActionIsDisabledAndAGameEditorRunsIt()
        {
            Allow(editsGame: true);
            Add("off", enabled: false);

            await Run("off");

            Assert.True(Started("off"));
        }

        [Fact]
        public async Task ExecAction_RunsTheInnerAction_WhenAPublicActionCallsABlockedOne()
        {
            Add("secret");
            AddAction("open", Step("s1", "ExecuteAction", new { Value = "t/secret" }));
            Allow(execute: _actions[^1].Id);

            await Run("open");

            Assert.True(Started("secret"));
        }

        [Fact]
        public async Task ExecAction_LooksOnlyAtEnabledAddons_WhenRunByName()
        {
            Allow(execute: Add("open"));

            await Run("open");

            Assert.All(Assert.IsAssignableFrom<GetActionsCommand[]>(Queries()), q => Assert.True(q.ExcludeDisabledAddons));
            Assert.NotEmpty(Queries());
        }

        [Fact]
        public async Task CallHook_LooksOnlyAtEnabledAddons_WhenAHookFires()
        {
            await _service.CallHookAsync(Hook.Load, new PlayerHookArgs { Player = _player });

            Assert.NotEmpty(Queries());
            Assert.All(Queries(), q => Assert.True(q.ExcludeDisabledAddons));
        }

        [Fact]
        public async Task CallHook_IncludesDisabledAddons_WhenUninstalling()
        {
            await _service.CallHookAsync(Hook.Uninstall, new AddonHookArgs { GameId = _lobby.GameId });

            Assert.NotEmpty(Queries());
            Assert.All(Queries(), q => Assert.False(q.ExcludeDisabledAddons));
        }
    }
}
