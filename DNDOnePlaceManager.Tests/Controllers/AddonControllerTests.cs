using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{    public class AddonControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly AddonController _controller;

        public AddonControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, contextUser: AnyUser());
        }

        private static AddonController CreateController(
            Mock<IMediator> mediatorMock,
            IServiceProvider? serviceProvider = null,
            Mock<IWebSocketManager>? wsMock = null,
            User? contextUser = null)
        {
            wsMock ??= new Mock<IWebSocketManager>();
            serviceProvider ??= new ServiceCollection().BuildServiceProvider();

            var controller = new AddonController(mediatorMock.Object, wsMock.Object, serviceProvider);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetPlayerCommandResponse { Player = player });

        // =========================================================================
        // GetActions
        // =========================================================================

        [Fact]
        public async Task GetActions_ReturnsBadRequest_WhenCommandResponseIsNotOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.WrongArguments, new List<ActionDto>()));

            // Act
            var result = await _controller.GetActions(Guid.NewGuid(), 0);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetActions_ReturnsOk_WhenCommandResponseIsOk()
        {
            // Arrange
            var actions = new List<ActionDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, actions));

            // Act
            var result = await _controller.GetActions(Guid.NewGuid(), 0);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(actions, ok.Value);
        }

        // =========================================================================
        // GetStepDefinitions
        // =========================================================================

        [Fact]
        public async Task GetStepDefinitions_ReturnsOk_WithEmptyDefinitions()
        {
            // Arrange — use a custom controller with an empty service provider
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var controller = CreateController(_mediator, serviceProvider, contextUser: AnyUser());

            // Act
            var result = await controller.GetStepDefinitions();

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetHooks
        // =========================================================================

        [Fact]
        public async Task GetHooks_ReturnsOk_WithAllHooks()
        {
            // Act
            var result = await _controller.GetHooks();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // GetAddonsToDownload
        // =========================================================================

        [Fact]
        public async Task GetAddonsToDownload_ReturnsOk_WithAddonList()
        {
            // Arrange
            var addons = new List<AddonDto> { new AddonDto { Key = "addon-1" } };
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonsFromRepositoryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(addons);

            // Act
            var result = await _controller.GetAddonsToDownload();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(addons, ok.Value);
        }

        // =========================================================================
        // GetAddons
        // =========================================================================

        [Fact]
        public async Task GetAddons_ReturnsOk_WithAddonList()
        {
            // Arrange
            var addons = new List<AddonDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(addons);

            // Act
            var result = await _controller.GetAddons(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(addons, ok.Value);
        }

        // =========================================================================
        // InstallAddon
        // =========================================================================

        [Fact]
        public async Task InstallAddon_ReturnsBadRequest_WhenNoSourceProvided()
        {
            // Arrange
            var request = new AddonController.InstallAddonRequest
            {
                File = null,
                Url = null,
                Key = null
            };

            // Act
            var result = await _controller.InstallAddon(Guid.NewGuid(), request);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task InstallAddon_ReturnsOk_WhenKeyProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            var request = new AddonController.InstallAddonRequest
            {
                File = null,
                Url = null,
                Key = "some-addon-key"
            };

            // Act
            var result = await _controller.InstallAddon(Guid.NewGuid(), request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // UninstallAddon
        // =========================================================================

        [Fact]
        public async Task UninstallAddon_ReturnsOk_WithCommandResponse()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.UninstallAddon(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(CommandResponse.Ok, ok.Value);
        }

        // =========================================================================
        // GetAction
        // =========================================================================

        [Fact]
        public async Task GetAction_ReturnsBadRequest_WhenCommandResponseIsNotOk()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<GetActionByIdCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.WrongArguments, new ActionDto()));

            // Act
            var result = await _controller.GetAction(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetAction_ReturnsOk_WhenCommandResponseIsOk()
        {
            // Arrange
            var actionDto = new ActionDto { Id = Guid.NewGuid() };
            _mediator.Setup(m => m.Send(It.IsAny<GetActionByIdCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, actionDto));

            // Act
            var result = await _controller.GetAction(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(actionDto, ok.Value);
        }

        // =========================================================================
        // GetCustomPanel
        // =========================================================================

        [Fact]
        public async Task GetCustomPanel_ReturnsOk_WithCardDto()
        {
            // Arrange
            var card = new CardDto { Id = Guid.NewGuid(), Name = "panel" };
            _mediator.Setup(m => m.Send(It.IsAny<GetCardCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(card);

            // Act
            var result = await _controller.GetCustomPanel(Guid.NewGuid(), "MyPanel");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(card, ok.Value);
        }

        // =========================================================================
        // GetCustomPanels
        // =========================================================================

        [Fact]
        public async Task GetCustomPanels_ReturnsOk_WithCardList()
        {
            // Arrange
            var cards = new List<CardDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, cards));

            // Act
            var result = await _controller.GetCustomPanels(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(cards, ok.Value);
        }
    }
}
