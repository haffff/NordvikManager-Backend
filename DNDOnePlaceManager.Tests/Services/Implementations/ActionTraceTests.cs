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
    public class ActionTraceTests : ActionServiceTestBase
    {
        // An action only the GM may run (GmPermission) has no row for a co-GM on the action
        // itself; Edit on the game is enough, like for the owner.
        [Fact]
        public async Task GmOnlyAction_RunByACoGm_Runs()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<bool> c, CancellationToken _) =>
                {
                    var check = (CheckPermissionsCommand)c;
                    return check.EntityId == _lobby.GameId && check.RequiredPermission == Permission.Edit;
                });
            AddAction("gm", Step("s1", "Log", new { Message = "hi" }));
            _actions[^1].GmPermission = Permission.Execute;

            var msgs = await Run("gm");

            Assert.Equal("Completed", msgs.Last()["state"]?.ToString());
        }

        [Fact]
        public async Task GmOnlyAction_RunByAPlayer_IsRefused()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            AddAction("gm", Step("s1", "Log", new { Message = "hi" }));
            _actions[^1].GmPermission = Permission.Execute;

            await Run("gm"); // no trace: a player who can't edit the game isn't traced

            Assert.Contains(_lobby.EventLog.GetEntries(), e => e.Category == "Permission");
        }

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
            AddAction("broken", Step("b1", "RunScript", new { Script = "throw new Error('broken');" }));
            AddAction("caller",
                Step("c1", "ExecuteAction", new { Value = "t/broken" }),
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
            AddAction("caller2", Step("c1", "ExecuteAction", new { Value = "t/nope" }));

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
            AddAction("bad", Step("f1", "RunScript", new { Script = "throw new Error('broken');" }));

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
