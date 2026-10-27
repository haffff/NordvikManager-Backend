using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.TurnOrder;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace NordvikManager.Tests.Controllers
{
    public class TurnOrderControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Player" };
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly Guid _mapId = Guid.NewGuid();

        private TurnOrderController Controller()
        {
            var controller = new TurnOrderController(_mediator.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "user-id", UserName = "user" };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private void IsPlayer(bool isPlayer = true) =>
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = isPlayer ? _player : null });

        [Fact]
        public async Task Get_ReturnsTheTurnOrderThisPlayerMaySee()
        {
            IsPlayer();
            var state = new TurnOrderDto { MapId = _mapId, Round = 2 };
            _mediator.Setup(m => m.Send(It.Is<GetTurnOrderCommand>(c => c.MapId == _mapId && c.GameId == _gameId && c.Player == _player), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(state);

            var ok = Assert.IsType<OkObjectResult>(await Controller().Get(_gameId, _mapId));

            Assert.Same(state, ok.Value);
        }

        [Fact]
        public async Task Get_NotAPlayer_IsUnauthorized()
        {
            IsPlayer(false);

            Assert.IsType<UnauthorizedObjectResult>(await Controller().Get(_gameId, _mapId));
        }

        [Fact]
        public async Task Get_WithoutReadOnTheMap_IsForbidden()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<GetTurnOrderCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new PermissionException(Permission.Read));

            Assert.IsType<ForbidResult>(await Controller().Get(_gameId, _mapId));
        }
    }
}
