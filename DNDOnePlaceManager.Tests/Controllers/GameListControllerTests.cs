using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.DeleteGame;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Application;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class GameListControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IConfiguration> _config = new();
        private readonly Mock<IWebSocketManager> _ws = new();
        private readonly GameListController _controller;

        public GameListControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(PlayerResponse(SomePlayer()));

            _controller = CreateController(_mediator, _config, _ws, RegularUser());
        }
        private static GameListController CreateController(
            Mock<IMediator> mediatorMock,
            Mock<IConfiguration>? configMock = null,
            Mock<IWebSocketManager>? wsMock = null,
            User? contextUser = null)
        {
            configMock ??= new Mock<IConfiguration>();
            wsMock ??= new Mock<IWebSocketManager>();
            return CreateController(mediatorMock, configMock.Object, wsMock.Object, contextUser);
        }

        private static GameListController CreateController(
            Mock<IMediator> mediatorMock,
            IConfiguration config,
            Mock<IWebSocketManager>? wsMock = null,
            User? contextUser = null)
        {
            wsMock ??= new Mock<IWebSocketManager>();
            var controller = new GameListController(mediatorMock.Object, config, wsMock.Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static GameListController CreateController(
            Mock<IMediator> mediatorMock,
            IConfiguration config,
            IWebSocketManager ws,
            User? contextUser = null)
        {
            var controller = new GameListController(mediatorMock.Object, config, ws);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AdminUser() => new User { Id = "admin-id", UserName = "admin", IsAdmin = true };
        private static User RegularUser() => new User { Id = "user-id", UserName = "user", IsAdmin = false };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player", IsOwner = true! };
        private static GetPlayerCommandResponse PlayerResponse(PlayerDTO? player) =>
                   new GetPlayerCommandResponse { Player = player! };

        // =========================================================================
        // GetGames
        // =========================================================================

        [Fact]
        public async Task GetGames_ReturnsOk_WithGameList()
        {
            // Arrange
            var games = new List<GameItemDTO> { new GameItemDTO() };
            _mediator.Setup(m => m.Send(It.IsAny<GetGameListCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetGameListCommandResponse { GameItemList = games });

            // Act
            var result = await _controller.GetGames();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(games, ok.Value);
        }

        // =========================================================================
        // DeleteGame
        // =========================================================================

        [Fact]
        public async Task DeleteGame_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<RemoveGameCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.DeleteGame(gameId);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetFeaturedAddons
        // =========================================================================        [Fact]
        [Fact]
        public async Task GetFeaturedAddons_ReturnsOk_WithFilteredAddons()
        {
            // Arrange
            var allAddons = new List<AddonDto>
            {
                new AddonDto { Key = "featured-addon" },
                new AddonDto { Key = "other-addon" }
            };

            // ConfigurationBinder.Get<T>() is an extension method — Moq can't stub it.
            // Build a real IConfiguration with the array stored as indexed keys.
            var realConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AddonsConfiguration:FeaturedAddons:0"] = "featured-addon"
                })
                .Build();

            _mediator.Setup(m => m.Send(It.IsAny<GetAddonsFromRepositoryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(allAddons);

            // Create a controller that uses the real config
            var controller = CreateController(_mediator, realConfig, _ws, RegularUser());

            // Act
            var result = await controller.GetFeaturedAddons();

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // AddGame
        // =========================================================================

        [Fact]
        public async Task AddGame_ReturnsBadRequest_WhenUserIsNotAdmin()
        {
            // Arrange — controller already has RegularUser context from constructor
            var command = new AddGameCommand { Name = "Game", PasswordRequired = false };

            // Act
            var result = await _controller.AddGame(command);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task AddGame_ReturnsBadRequest_WhenMediatorReturnsFalse()
        {
            // Arrange
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AddGameCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
            var controller = CreateController(mediator, contextUser: AdminUser());
            var command = new AddGameCommand { Name = "Game", PasswordRequired = false };

            // Act
            var result = await controller.AddGame(command);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task AddGame_ReturnsOk_WhenAdminAndMediatorReturnsTrue()
        {
            // Arrange
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AddGameCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            var controller = CreateController(mediator, contextUser: AdminUser());
            var command = new AddGameCommand { Name = "Game", PasswordRequired = false };

            // Act
            var result = await controller.AddGame(command);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // JoinGame
        // =========================================================================

        [Fact]
        public async Task JoinGame_ReturnsBadRequest_WhenMediatorReturnsNull()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<AddPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Guid?)null);
            var command = new AddPlayerCommand { GameID = gameId };

            // Act
            var result = await _controller.JoinGame(command);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task JoinGame_ReturnsOk_WhenPlayerJoinsSuccessfully()
        {
            // Arrange
            var gameId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var player = SomePlayer();

            _mediator.Setup(m => m.Send(It.IsAny<AddPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Guid?)playerId);
            // GetPlayerCommand — used inside AddDefaultCharacterSheet
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = player });
            // GetSystemPlayerCommand → returns PlayerDTO (treated as systemPlayer)
            _mediator.Setup(m => m.Send(It.IsAny<GetSystemPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(player);
            // GetPropertiesByQueryCommand — return empty list so useDefaultCharacterSheets is null → early exit
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<PropertyDTO>());

            var command = new AddPlayerCommand { GameID = gameId };

            // Act
            var result = await _controller.JoinGame(command);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal((Guid?)playerId, ok.Value);
        }

        // =========================================================================
        // GetVersionInfo
        // =========================================================================

        [Fact]
        public async Task GetVersionInfo_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange — controller already has RegularUser context from constructor

            // Act
            var result = await _controller.GetVersionInfo();

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetVersionInfo_ReturnsOk_WhenUserIsAdmin()
        {
            // Arrange
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetVersionInfoCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new VersionInfoDTO { Version = "1.0.0" });
            var controller = CreateController(mediator, contextUser: AdminUser());

            // Act
            var result = await controller.GetVersionInfo();

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
