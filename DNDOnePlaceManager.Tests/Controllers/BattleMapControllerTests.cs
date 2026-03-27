using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayout;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayouts;
using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{    public class BattleMapControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly BattleMapController _controller;

        public BattleMapControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, contextUser: AnyUser());
        }

        private static BattleMapController CreateController(
            Mock<IMediator> mediatorMock,
            Mock<IWebSocketManager>? wsMock = null,
            User? contextUser = null)
        {
            wsMock ??= new Mock<IWebSocketManager>();
            var controller = new BattleMapController(wsMock.Object, mediatorMock.Object);
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
        // GetPlayer
        // =========================================================================

        [Fact]
        public async Task GetPlayer_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetPlayer(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetPlayer_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var player = SomePlayer();
            SetupPlayer(_mediator, player);

            // Act
            var result = await _controller.GetPlayer(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(player, ok.Value);
        }

        // =========================================================================
        // GetFullGame
        // =========================================================================

        [Fact]
        public async Task GetFullGame_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetFullGame(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetFullGame_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var gameResponse = new DndOnePlaceManager.Application.Commands.BattleMap.GetGame.GetGameCommandResponse();
            _mediator.Setup(m => m.Send(It.IsAny<GetGameCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(gameResponse);

            // Act
            var result = await _controller.GetFullGame(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(gameResponse, ok.Value);
        }

        // =========================================================================
        // GetChat
        // =========================================================================

        [Fact]
        public async Task GetChat_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetChat(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetChat_ReturnsOk_WithMessages()
        {
            // Arrange
            var messages = new List<DndOnePlaceManager.Application.DataTransferObjects.Chat.MessageDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetMessagesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(messages);

            // Act
            var result = await _controller.GetChat(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(messages, ok.Value);
        }

        // =========================================================================
        // GetLayout
        // =========================================================================

        [Fact]
        public async Task GetLayout_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetLayout(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetLayout_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var layoutDto = new LayoutDTO { Id = Guid.NewGuid(), Name = "layout" };
            _mediator.Setup(m => m.Send(It.IsAny<GetLayoutCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(layoutDto);

            // Act
            var result = await _controller.GetLayout(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(layoutDto, ok.Value);
        }

        // =========================================================================
        // GetBattlemap
        // =========================================================================

        [Fact]
        public async Task GetBattlemap_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetBattlemap(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetBattlemap_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var bmDto = new BattleMapDto { Id = Guid.NewGuid() };
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(bmDto);

            // Act
            var result = await _controller.GetBattlemap(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(bmDto, ok.Value);
        }

        // =========================================================================
        // GetBattlemaps
        // =========================================================================

        [Fact]
        public async Task GetBattlemaps_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetBattlemaps(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetBattlemaps_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var bmList = new List<BattleMapDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(bmList);

            // Act
            var result = await _controller.GetBattlemaps(Guid.NewGuid());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetLayouts
        // =========================================================================

        [Fact]
        public async Task GetLayouts_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetLayouts(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetLayouts_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var layouts = new List<LayoutDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetLayoutsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(layouts);

            // Act
            var result = await _controller.GetLayouts(Guid.NewGuid());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetCards
        // =========================================================================

        [Fact]
        public async Task GetCards_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetCards(Guid.NewGuid());

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetCards_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var cards = new List<CardDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, cards));

            // Act
            var result = await _controller.GetCards(Guid.NewGuid());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetTree
        // =========================================================================

        [Fact]
        public async Task GetTree_ReturnsUnauthorized_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetTree(Guid.NewGuid(), "element");

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetTree_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var tree = new List<TreeEntryDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetTreeEntriesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(tree);

            // Act
            var result = await _controller.GetTree(Guid.NewGuid(), "element");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(tree, ok.Value);
        }
    }
}
