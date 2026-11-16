using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.Application.Commands.Addons.GetActionAddon;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the try/catch added around mediator.Send this session — before this,
    // network-level failures (DNS, connection refused, timeout) threw out of Execute and
    // silently faulted the whole action, with no downstream step (e.g. an If branching on
    // Output.Success) ever observing the failure. ProxyCommandHandler itself already
    // reports HTTP-level failures (4xx/5xx) without throwing, so this only needs to cover
    // the mediator-throws path.
    public class SendRequestActionStepTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly SendRequestActionStep _step = new();

        private static ActionStep MakeStep(string url = "https://unreachable.example.invalid", string output = "output") => new ActionStep
        {
            Type = "SendRequest",
            Data = JObject.FromObject(new
            {
                TargetUrl = url,
                Output = output,
            }),
        };

        [Fact]
        public async Task Execute_MediatorThrows_StoresFailureResultInsteadOfPropagating()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new System.Net.Sockets.SocketException());

            var variables = Run(Guid.NewGuid());

            // Must not throw — a network failure should be captured as data, not an exception.
            await _step.Execute(_mediator.Object, variables, TestLobby.Create(), MakeStep());

            Assert.True(variables.ContainsKey("output"));
            var result = Assert.IsType<ProxyCommandResult>(variables["output"]);
            Assert.False(result.Success);
            Assert.Equal(0, result.StatusCode);
            Assert.NotNull(result.ResponseBody);
            Assert.Equal(ProxyReasons.NetworkError, result.Reason);
        }

        // A refused request (here the host rules) tells the game's GMs which action of which
        // addon tried to reach where, and why: the origin only, never the path.
        [Fact]
        public async Task Execute_Refused_TellsTheGms()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProxyCommandResult { Success = false, StatusCode = 400, Reason = ProxyReasons.BlockedHost });
            var actionId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.Is<GetActionAddonCommand>(c => c.ActionId == actionId), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("trilium");
            var lobby = NoticeLobby.Create(_mediator.Object, out var gmSent, out var playerSent);
            var variables = Run(actionId);

            await _step.Execute(_mediator.Object, variables, lobby, MakeStep("https://notes.example.com:8443/etapi/notes?secret=1"));

            var notice = Assert.Single(gmSent);
            Assert.Equal(WebSocketCommandNames.AddonNetworkNotice, notice.Command);
            Assert.Equal("action", (string)notice.Data["source"]);
            Assert.Equal("trilium", (string)notice.Data["addon"]);
            Assert.Equal("Import", (string)notice.Data["action"]);
            Assert.Equal(actionId.ToString(), (string)notice.Data["actionId"]);
            Assert.Equal("https://notes.example.com:8443", (string)notice.Data["origin"]);
            Assert.Equal(ProxyReasons.BlockedHost, (string)notice.Data["reason"]);
            Assert.Empty(playerSent);
        }

        // A URL can carry credentials before the host: the notice shows the origin without them.
        [Fact]
        public async Task Execute_Refused_NeverTellsTheGmsTheUrlsCredentials()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProxyCommandResult { Success = false, StatusCode = 403, Reason = ProxyReasons.UndeclaredHost });
            var lobby = NoticeLobby.Create(_mediator.Object, out var gmSent, out _);

            await _step.Execute(_mediator.Object, Run(Guid.NewGuid()), lobby, MakeStep("https://user:apitoken@api.example.com/x"));

            Assert.Equal("https://api.example.com", (string)Assert.Single(gmSent).Data["origin"]);
        }

        // The handler holds the action to its addon's declared network, so it's told which action.
        [Fact]
        public async Task Execute_SendsTheActionsId()
        {
            ProxyCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ProxyCommandResult>, CancellationToken>((c, _) => sent = (ProxyCommand)c)
                     .ReturnsAsync(new ProxyCommandResult { Success = true, StatusCode = 200 });
            var actionId = Guid.NewGuid();

            await _step.Execute(_mediator.Object, Run(actionId), TestLobby.Create(), MakeStep());

            Assert.Equal(actionId, sent.ActionId);
        }

        // Outside the engine (no context) the step can't tell whose request it is: refused.
        [Fact]
        public async Task Execute_WithoutTheEnginesContext_IsRefused()
        {
            var variables = new Dictionary<string, object> { ["actionId"] = Guid.NewGuid().ToString() };

            await _step.Execute(_mediator.Object, variables, TestLobby.Create(), MakeStep());

            Assert.Equal(ProxyReasons.UndeclaredHost, ((ProxyCommandResult)variables["output"]).Reason);
            _mediator.Verify(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static Dictionary<string, object> Run(Guid actionId, string name = "Import") =>
            new() { [ActionRunContext.Key] = new ActionRunContext(actionId, name) };

        // From the browser version: the site refused the browser's request or didn't answer.
        [Fact]
        public async Task Execute_BrowserRefused_TellsTheGms()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProxyCommandResult { Success = false, StatusCode = 0, Reason = ProxyReasons.BrowserRefused });
            var lobby = NoticeLobby.Create(_mediator.Object, out var gmSent, out _);

            await _step.Execute(_mediator.Object, Run(Guid.NewGuid()), lobby, MakeStep("https://api.example.com/x"));

            Assert.Equal(ProxyReasons.BrowserRefused, (string)Assert.Single(gmSent).Data["reason"]);
        }

        [Fact]
        public async Task Execute_Succeeds_TellsNoOne()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProxyCommandResult { Success = true, StatusCode = 200 });
            var lobby = NoticeLobby.Create(_mediator.Object, out var gmSent, out _);

            await _step.Execute(_mediator.Object, Run(Guid.NewGuid()), lobby, MakeStep());

            Assert.Empty(gmSent);
        }

        [Fact]
        public async Task Execute_MediatorSucceeds_StoresReturnedResultUnchanged()
        {
            var expected = new ProxyCommandResult { Success = true, StatusCode = 200, ResponseBody = "{\"ok\":true}" };
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expected);

            var variables = Run(Guid.NewGuid());

            await _step.Execute(_mediator.Object, variables, TestLobby.Create(), MakeStep());

            Assert.Same(expected, variables["output"]);
        }
    }
}
