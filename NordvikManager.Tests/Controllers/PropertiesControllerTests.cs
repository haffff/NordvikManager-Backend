using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Properties.AddProperties;
using NordvikManager.Application.Commands.Properties.GetProperties;
using NordvikManager.Application.Commands.Properties.GetPropertiesByQuery;
using NordvikManager.Application.Commands.Properties.UpdateProperties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
{
    public class PropertiesControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly PropertiesController _controller;

        public PropertiesControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, AnyUser());
        }

        private static PropertiesController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var controller = new PropertiesController(mediatorMock.Object, new Mock<NordvikManager.Services.ILobbyService>().Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static GetPlayerCommandResponse WithPlayer(PlayerDTO? player) =>
            new GetPlayerCommandResponse { Player = player };

        private void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(WithPlayer(player));

        private static readonly Dictionary<string, Func<PropertiesController, Task<IActionResult>>> Actions = new()
        {
            ["GetProperties"] = c => c.GetProperties(Guid.NewGuid(), Guid.NewGuid()),
            ["QueryProperties"] = c => c.QueryProperties(Guid.NewGuid(), Guid.NewGuid().ToString(), null, null, null),
            ["GetSelectedIds"] = c => c.GetSelectedIds(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>()),
            ["AddBulkProperties"] = c => c.AddBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>()),
            ["UpdateBulkProperties"] = c => c.UpdateBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>()),
        };

        public static TheoryData<string> ActionNames() => new(Actions.Keys);

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task Action_ReturnsUnauthorized_WhenPlayerIsNull(string action)
        {
            SetupPlayer(_mediator, null);

            Assert.IsType<UnauthorizedObjectResult>(await Actions[action](_controller));
        }

        // Reads answer what the command found; bulk writes answer Ok when the command succeeds.
        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task Action_ReturnsOk_WhenTheCommandSucceeds(string action)
        {
            var properties = new List<PropertyDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(properties);
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(properties);
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertiesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);

            var ok = Assert.IsType<OkObjectResult>(await Actions[action](_controller));
            if (action is "GetProperties" or "QueryProperties") Assert.Same(properties, ok.Value);
        }

        // Invalid properties: the command's refusal reaches the middleware (400).
        [Theory]
        [InlineData("AddBulkProperties")]
        [InlineData("UpdateBulkProperties")]
        public async Task BulkWrite_LetsTheCommandsRefusalThrough(string action)
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new WrongArgumentsException(nameof(AddPropertiesCommand.Properties)));
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new WrongArgumentsException(nameof(UpdatePropertiesCommand.Properties)));

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Actions[action](_controller));
        }
    }
}
