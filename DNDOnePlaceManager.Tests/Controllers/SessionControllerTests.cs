using DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using DNDOnePlaceManager.Services.Implementations;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class SessionControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IWebRTCSessionService> _webRtcSession = new();
        private readonly Guid _gameId = Guid.NewGuid();

        public SessionControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true } });
            _mediator.Setup(m => m.Send(It.IsAny<GetGameCentralSessionIdCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("central-session-1");
            _mediator.Setup(m => m.Send(It.IsAny<GetSystemPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PlayerDTO { Id = Guid.NewGuid(), Name = "System" });
        }

        private SessionController CreateController()
        {
            var controller = new SessionController(
                _mediator.Object,
                new LobbyRegistry(),
                _webRtcSession.Object,
                LobbyScopeFactory(),
                new Mock<ICentralServerService>().Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "gm-id", UserName = "gm" };
            httpContext.Request.Headers.Cookie = $"CentralToken={ValidCentralToken()}";
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        // GameLobby resolves these from its own scope on construction
        // (same setup as PlaylistControllerTests.MakeLobby).
        private static IServiceScopeFactory LobbyScopeFactory()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
            return scopeFactory.Object;
        }

        private static string ValidCentralToken()
        {
            var key = new SymmetricSecurityKey(new byte[32]);
            var token = new JwtSecurityToken(
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task StartSession_ReturnsConflictWithCentralMessage_WhenCentralRejectsGm()
        {
            const string message = "Your NordvikManager server is outdated (protocol 1, minimum 2). Please update it to start a session.";
            _webRtcSession.Setup(s => s.StartSessionAsync(_gameId, "central-session-1", It.IsAny<string>()))
                          .ThrowsAsync(new SignalingAuthException(message));

            var result = await CreateController().StartSession(_gameId);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var error = conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value);
            Assert.Equal(message, error);
        }

        [Fact]
        public async Task StartSession_ReturnsOk_WhenSignalingStarts()
        {
            _webRtcSession.Setup(s => s.StartSessionAsync(_gameId, "central-session-1", It.IsAny<string>()))
                          .Returns(Task.CompletedTask);

            var result = await CreateController().StartSession(_gameId);

            Assert.IsType<OkObjectResult>(result);
        }
    }
}
