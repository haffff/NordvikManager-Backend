using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled;
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
using DNDOnePlaceManager.Services;
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
{
    public class AddonControllerTests
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
            Mock<ILobbyService>? lobbyMock = null,
            User? contextUser = null)
        {
            lobbyMock ??= new Mock<ILobbyService>();
            serviceProvider ??= new ServiceCollection().BuildServiceProvider();

            var controller = new AddonController(mediatorMock.Object, lobbyMock.Object, serviceProvider);
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
        // GetInstalledAddons
        // =========================================================================

        [Fact]
        public async Task GetInstalledAddons_ReturnsOk_WithAddonList()
        {
            // Arrange
            var addons = new List<AddonDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(addons);

            // Act
            var result = await _controller.GetInstalledAddons(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(addons, ok.Value);
        }

        // =========================================================================
        // GetRepository
        // =========================================================================

        [Fact]
        public async Task GetRepository_ReturnsOk_WithAddonList()
        {
            // Arrange
            var addons = new List<AddonDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonsFromRepositoryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(addons);

            // Act
            var result = await _controller.GetRepository(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(addons, ok.Value);
        }

        // =========================================================================
        // GetAddons (legacy)
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
        public async Task InstallAddon_ReturnsBadRequest_WhenKeyMissing()
        {
            // Act
            var result = await _controller.InstallAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = null });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task InstallAddon_ReturnsOk_WhenKeyProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            // Act
            var result = await _controller.InstallAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = "dnd5e" });

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task InstallAddon_ReturnsBadRequest_WhenCommandFails()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.WrongArguments, Guid.Empty));

            // Act
            var result = await _controller.InstallAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = "dnd5e" });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // UpdateAddon
        // =========================================================================

        [Fact]
        public async Task UpdateAddon_ReturnsOk_WhenKeyProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.Is<InstallAddonCommand>(c => c.Reinstall), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            // Act
            var result = await _controller.UpdateAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = "dnd5e" });

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UpdateAddon_ReturnsBadRequest_WhenKeyMissing()
        {
            // Act
            var result = await _controller.UpdateAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = null });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // UninstallAddon
        // =========================================================================

        [Fact]
        public async Task UninstallAddon_ReturnsOk_WhenAddonIdProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.UninstallAddon(Guid.NewGuid(),
                new AddonController.UninstallAddonRequest { AddonId = Guid.NewGuid().ToString() });

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UninstallAddon_ReturnsOk_WhenKeyProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.UninstallAddon(Guid.NewGuid(),
                new AddonController.UninstallAddonRequest { AddonId = "dnd5e" });

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UninstallAddon_ReturnsBadRequest_WhenAddonIdMissing()
        {
            // Act
            var result = await _controller.UninstallAddon(Guid.NewGuid(),
                new AddonController.UninstallAddonRequest { AddonId = null });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // SetEnabled
        // =========================================================================

        [Fact]
        public async Task SetEnabled_ReturnsOk_WhenAddonIdAndEnabledProvided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<SetAddonEnabledCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.SetEnabled(Guid.NewGuid(),
                new AddonController.SetEnabledRequest { AddonId = "dnd5e", Enabled = true });

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task SetEnabled_ReturnsBadRequest_WhenAddonIdMissing()
        {
            // Act
            var result = await _controller.SetEnabled(Guid.NewGuid(),
                new AddonController.SetEnabledRequest { AddonId = null, Enabled = true });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetEnabled_ReturnsBadRequest_WhenCommandFails()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<SetAddonEnabledCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.NoResource);

            // Act
            var result = await _controller.SetEnabled(Guid.NewGuid(),
                new AddonController.SetEnabledRequest { AddonId = "dnd5e", Enabled = false });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // InstallFromFile
        // =========================================================================

        [Fact]
        public async Task InstallFromFile_ReturnsBadRequest_WhenDataMissing()
        {
            // Act
            var result = await _controller.InstallFromFile(Guid.NewGuid(),
                new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = null });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task InstallFromFile_ReturnsBadRequest_WhenDataIsNotBase64()
        {
            // Act
            var result = await _controller.InstallFromFile(Guid.NewGuid(),
                new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = "not-base64!!!" });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task InstallFromFile_ReturnsOk_WhenValidBase64Provided()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });

            // Act
            var result = await _controller.InstallFromFile(Guid.NewGuid(),
                new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = base64 });

            // Assert
            Assert.IsType<OkResult>(result);
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

