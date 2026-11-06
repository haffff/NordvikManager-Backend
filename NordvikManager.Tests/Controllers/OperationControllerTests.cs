using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Tests.Controllers
{
    public class OperationControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly Guid _gameId = Guid.NewGuid();

        private OperationController Controller(bool isPlayer = true)
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = isPlayer ? _player : null });
            var controller = new OperationController(_mediator.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "user-id", UserName = "gm" };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static JObject Body(IActionResult result) =>
            JObject.FromObject(Assert.IsType<OkObjectResult>(result).Value!);

        [Fact]
        public async Task Status_ReturnsTheOutcome_ForThePlayerWhoStartedIt()
        {
            var op = Guid.NewGuid();
            OperationProgressPublisher.Begin(op, _player);
            await OperationProgressPublisher.Complete(null, _player, op, "Addon installed", "Fifth Edition");

            var body = Body(await Controller().Status(_gameId, op));

            Assert.Equal("complete", (string?)body["status"]);
            Assert.Equal("Addon installed", (string?)body["title"]);
            Assert.Equal("Fifth Edition", (string?)body["description"]);
        }

        [Fact]
        public async Task Status_ReturnsRunning_WhileItRuns()
        {
            var op = Guid.NewGuid();
            OperationProgressPublisher.Begin(op, _player);

            Assert.Equal("running", (string?)Body(await Controller().Status(_gameId, op))["status"]);
        }

        [Fact]
        public async Task Status_ReturnsUnknown_ForAnotherPlayersOrAForgottenOperation()
        {
            var op = Guid.NewGuid();
            OperationProgressPublisher.Begin(op, new PlayerDTO { Id = Guid.NewGuid() });

            Assert.Equal("unknown", (string?)Body(await Controller().Status(_gameId, op))["status"]);
            Assert.Equal("unknown", (string?)Body(await Controller().Status(_gameId, Guid.NewGuid()))["status"]);
        }

        [Fact]
        public async Task Status_IsUnauthorized_ForSomeoneNotInTheGame()
        {
            Assert.IsType<UnauthorizedObjectResult>(await Controller(isPlayer: false).Status(_gameId, Guid.NewGuid()));
        }
    }
}
