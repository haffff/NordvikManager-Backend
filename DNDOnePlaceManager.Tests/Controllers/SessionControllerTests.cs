using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.GameExists;
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
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    // Starting a session for a game that no longer exists answered 401 ("You are not a
    // player in this game"). The frontend reads 401 as "login expired", sent the user to
    // log in, then retried the same game (?game= in the URL): an endless login loop. A
    // missing game is 404 and "not a player" is 403; 401 stays for a missing login.
    public class SessionControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();

        private SessionController Controller(User? user)
        {
            var controller = new SessionController(
                _mediator.Object,
                new Mock<ILobbyRegistry>().Object,
                new Mock<IWebRTCSessionService>().Object,
                new Mock<IServiceScopeFactory>().Object,
                new Mock<ICentralServerService>().Object);
            var httpContext = new DefaultHttpContext();
            if (user != null) httpContext.Items["User"] = user;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User SomeUser() => new() { Id = "user-1", UserName = "alice" };

        private void GameExists(bool exists) =>
            _mediator.Setup(m => m.Send(It.IsAny<GameExistsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(exists);

        private void Player(PlayerDTO? player) =>
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = player });

        [Fact]
        public async Task StartSession_GameNoLongerExists_ReturnsNotFound()
        {
            GameExists(false);
            Player(null);

            var result = await Controller(SomeUser()).StartSession(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task StartSession_NotAPlayerInAnExistingGame_ReturnsForbidden()
        {
            GameExists(true);
            Player(null);

            var result = await Controller(SomeUser()).StartSession(Guid.NewGuid());

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        }

        [Fact]
        public async Task StartSession_NotLoggedIn_ReturnsUnauthorized()
        {
            GameExists(true);
            Player(null);

            var result = await Controller(null).StartSession(Guid.NewGuid());

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
