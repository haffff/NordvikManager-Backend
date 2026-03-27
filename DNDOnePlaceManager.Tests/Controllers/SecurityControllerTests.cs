using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class SecurityControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly SecurityController _controller;
        private static readonly User _anyUser = new() { Id = "user-id", UserName = "user" };

        public SecurityControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = SomePlayer() });

            _controller = CreateController(_mediator, _anyUser);
        }

        private static SecurityController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var controller = new SecurityController(mediatorMock.Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };

        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static GetPlayerCommandResponse PlayerResponse(PlayerDTO? player) =>
            new GetPlayerCommandResponse { Player = player };

        // =========================================================================
        // GetPermissions
        // =========================================================================

        [Fact]
        public async Task GetPermissions_ReturnsOk_WhenPlayerExistsAndPermissionsReturned()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            var entityId = Guid.NewGuid();
            var permissions = new Dictionary<Guid, Permission> { { entityId, Permission.Read } };

            _mediator.Setup(m => m.Send(It.IsAny<GetPermissionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(permissions);

            // Act
            var result = await _controller.GetPermissions(gameId, entityId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(permissions, ok.Value);
        }

        [Fact]
        public async Task GetPermissions_SendsGetPlayerCommand_WithCorrectGameIdAndUser()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            var entityId = Guid.NewGuid();
            var user = _anyUser;

            GetPlayerCommand? capturedCmd = null;
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<GetPlayerCommandResponse>, CancellationToken>((cmd, _) => capturedCmd = (GetPlayerCommand)cmd)
                     .ReturnsAsync(PlayerResponse(SomePlayer()));
            _mediator.Setup(m => m.Send(It.IsAny<GetPermissionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Dictionary<Guid, Permission>());

            // Act
            await _controller.GetPermissions(gameId, entityId);

            // Assert
            Assert.NotNull(capturedCmd);
            Assert.Equal(gameId, capturedCmd!.GameID);
            Assert.Equal(user, capturedCmd.User);
        }
    }
}
