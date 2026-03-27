using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{    public class MapControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly MapController _controller;

        public MapControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(PlayerResponse(SomePlayer()));

            _controller = CreateController(_mediator, AnyUser());
        }

        private static MapController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var controller = new MapController(mediatorMock.Object);
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
        // GetMap
        // =========================================================================

        [Fact]
        public async Task GetMap_ReturnsOk_WhenPlayerExistsAndMapReturned()
        {
            // Arrange
            var mapId = Guid.NewGuid();
            var gameId = Guid.NewGuid();
            var mapDto = new MapDTO();

            _mediator.Setup(m => m.Send(It.IsAny<GetMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(mapDto);

            // Act
            var result = await _controller.GetMap(mapId, gameId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(mapDto, ok.Value);
        }

        [Fact]
        public async Task GetMap_SendsCorrectMapId()
        {
            // Arrange
            var mapId = Guid.NewGuid();
            var gameId = Guid.NewGuid();
            GetMapCommand? capturedCmd = null;

            _mediator.Setup(m => m.Send(It.IsAny<GetMapCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<MapDTO>, CancellationToken>((cmd, _) => capturedCmd = (GetMapCommand)cmd)
                     .ReturnsAsync(new MapDTO());

            // Act
            await _controller.GetMap(mapId, gameId);

            // Assert
            Assert.NotNull(capturedCmd);
            Assert.Equal(mapId, capturedCmd!.Id);
        }

        // =========================================================================
        // GetAllFlat
        // =========================================================================

        [Fact]
        public async Task GetAllFlat_ReturnsOk_WhenPlayerExistsAndMapsReturned()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            var flatMaps = new List<MapDTO>(); // Updated to match the expected type

            _mediator.Setup(m => m.Send(It.IsAny<GetFlatMapsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(flatMaps as List<MapDTO>); // Explicitly cast to List<MapDTO>

            // Act
            var result = await _controller.GetAllFlat(gameId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(flatMaps, ok.Value);
        }

        [Fact]
        public async Task GetAllFlat_SendsCorrectGameId()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            GetFlatMapsCommand? capturedCmd = null;

            _mediator.Setup(m => m.Send(It.IsAny<GetFlatMapsCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<List<MapDTO>>, CancellationToken>((cmd, _) => capturedCmd = (GetFlatMapsCommand)cmd)
                     .ReturnsAsync(new List<MapDTO>());

            // Act
            await _controller.GetAllFlat(gameId);

            // Assert
            Assert.NotNull(capturedCmd);
            Assert.Equal(gameId, capturedCmd!.GameID);
        }
    }
}
