using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Properties.UpdateProperties;
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
{    public class PropertiesControllerTests
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
            var controller = new PropertiesController(mediatorMock.Object);
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

        // =========================================================================
        // GetProperties
        // =========================================================================

        [Fact]
        public async Task GetProperties_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetProperties(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetProperties_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var properties = new List<PropertyDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(properties);

            // Act
            var result = await _controller.GetProperties(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(properties, ok.Value);
        }

        // =========================================================================
        // QueryProperties
        // =========================================================================

        [Fact]
        public async Task QueryProperties_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.QueryProperties(Guid.NewGuid(), Guid.NewGuid().ToString(), null, null, null);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task QueryProperties_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var properties = new List<PropertyDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(properties);

            // Act
            var result = await _controller.QueryProperties(Guid.NewGuid(), Guid.NewGuid().ToString(), null, null, null);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(properties, ok.Value);
        }

        // =========================================================================
        // GetSelectedIds (obsolete)
        // =========================================================================

        [Fact]
        public async Task GetSelectedIds_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetSelectedIds(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetSelectedIds_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var properties = new List<PropertyDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(properties);

            // Act
            var result = await _controller.GetSelectedIds(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // AddBulkProperties
        // =========================================================================

        [Fact]
        public async Task AddBulkProperties_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.AddBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task AddBulkProperties_ReturnsBadRequest_WhenCommandResponseIsNotOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.WrongArguments);

            // Act
            var result = await _controller.AddBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AddBulkProperties_ReturnsOk_WhenCommandResponseIsOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.AddBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // UpdateBulkProperties
        // =========================================================================

        [Fact]
        public async Task UpdateBulkProperties_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.UpdateBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task UpdateBulkProperties_ReturnsBadRequest_WhenCommandResponseIsNotOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.WrongArguments);

            // Act
            var result = await _controller.UpdateBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateBulkProperties_ReturnsOk_WhenCommandResponseIsOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.UpdateBulkProperties(Guid.NewGuid(), Array.Empty<PropertyDTO>());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
