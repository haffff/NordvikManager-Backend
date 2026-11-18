using NordvikManager.Application.Commands.Properties.Proxy;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    // Which action is running decides which addon's network holds its requests. The engine
    // says so with a context steps can't forge: an action that sets its own "actionId" (any
    // step's Output can) is still the action it is.
    public class ActionRunContextTests : ActionServiceTestBase
    {
        [Fact]
        public async Task SendRequest_CarriesTheRunningActionsId_WhateverItsVariablesSay()
        {
            ProxyCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<ProxyCommandResult>, CancellationToken>((c, _) => sent = (ProxyCommand)c)
                .ReturnsAsync(new ProxyCommandResult { Success = true, StatusCode = 200 });
            AddAction("a",
                Step("spoof", "SetVariable", new { Name = "actionId", Value = Guid.NewGuid().ToString() }),
                Step("req", "SendRequest", new { TargetUrl = "https://api.example.com/x", Output = "out" }));

            await Run("a");

            Assert.NotNull(sent);
            Assert.Equal(_actions[0].Id, sent.ActionId);
        }

        // An action an addon's action runs (Execute Action) is held to that addon's network too:
        // its request carries who called it, so a GM-made or undeclared action can't carry the
        // addon's request somewhere its addon didn't declare.
        [Fact]
        public async Task SendRequest_InAnActionRunByAnother_CarriesItsCaller()
        {
            ProxyCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<ProxyCommandResult>, CancellationToken>((c, _) => sent = (ProxyCommand)c)
                .ReturnsAsync(new ProxyCommandResult { Success = true, StatusCode = 200 });
            AddAction("caller", Step("call", "ExecuteAction", new { Value = "t/callee" }));
            AddAction("callee", Step("req", "SendRequest", new { TargetUrl = "https://api.example.com/x", Output = "out" }));

            await Run("caller");

            Assert.NotNull(sent);
            Assert.Equal(_actions[1].Id, sent.ActionId);
            Assert.Equal(new[] { _actions[0].Id!.Value }, sent.CallerActionIds);
        }

        // Overwriting the context itself leaves no context: the request doesn't go out.
        [Fact]
        public async Task SendRequest_WithItsContextOverwritten_DoesNotGoOut()
        {
            ProxyCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<ProxyCommandResult>, CancellationToken>((c, _) => sent = (ProxyCommand)c)
                .ReturnsAsync(new ProxyCommandResult { Success = true, StatusCode = 200 });
            AddAction("a",
                Step("spoof", "SetVariable", new { Name = "__actionRun", Value = Guid.NewGuid().ToString() }),
                Step("req", "SendRequest", new { TargetUrl = "https://api.example.com/x", Output = "out" }));

            await Run("a");

            Assert.Null(sent);
        }
    }
}
