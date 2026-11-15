using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebSockets.Handlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;

namespace NordvikManager.Tests
{
    // A real GameLobby for tests of code that broadcasts through lobby.HandlePostCommand (a mocked
    // ILobbyService wouldn't change ActivePlaylistPlaybacks or reach the players). Its scopes hand
    // out the action service (a bare mock unless one is given; the constructor asks for it with
    // GetRequiredService, so a bare Mock.Of<IServiceScopeFactory>() isn't enough), no websocket
    // handlers and, when given, the mediator HandlePostCommand resolves in its own scope.
    // Anything else they're asked for is null.
    internal static class TestLobby
    {
        public static GameLobby Create(IMediator? mediator = null, IActionProcessingService? actions = null)
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(actions ?? new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<IWebSocketHandler>)))
                    .Returns(new List<IWebSocketHandler>());
            if (mediator != null)
                provider.Setup(p => p.GetService(typeof(IMediator))).Returns(mediator);

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }
    }
}
