using DndOnePlaceManager.Application.Commands.Properties.Proxy;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
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

        private static ActionStep MakeStep(string output = "output") => new ActionStep
        {
            Type = "SendRequest",
            Data = JObject.FromObject(new
            {
                TargetUrl = "https://unreachable.example.invalid",
                Output = output,
            }),
        };

        // GameLobby's constructor eagerly resolves IActionProcessingService via
        // GetRequiredService (throws on null) and a couple of optional services via
        // GetService (tolerates null), so a bare Mock.Of<IServiceScopeFactory>() isn't
        // enough — it needs a scope/provider chain wired through to a mocked
        // IActionProcessingService.
        private static GameLobby MakeLobby()
        {
            var actionProcessingService = new Mock<IActionProcessingService>();

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(actionProcessingService.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        [Fact]
        public async Task Execute_MediatorThrows_StoresFailureResultInsteadOfPropagating()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new System.Net.Sockets.SocketException());

            var variables = new Dictionary<string, object>();

            // Must not throw — a network failure should be captured as data, not an exception.
            await _step.Execute(_mediator.Object, variables, MakeLobby(), MakeStep());

            Assert.True(variables.ContainsKey("output"));
            var result = Assert.IsType<ProxyCommandResult>(variables["output"]);
            Assert.False(result.Success);
            Assert.Equal(0, result.StatusCode);
            Assert.NotNull(result.ResponseBody);
        }

        [Fact]
        public async Task Execute_MediatorSucceeds_StoresReturnedResultUnchanged()
        {
            var expected = new ProxyCommandResult { Success = true, StatusCode = 200, ResponseBody = "{\"ok\":true}" };
            _mediator.Setup(m => m.Send(It.IsAny<ProxyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expected);

            var variables = new Dictionary<string, object>();

            await _step.Execute(_mediator.Object, variables, MakeLobby(), MakeStep());

            Assert.Same(expected, variables["output"]);
        }
    }
}
