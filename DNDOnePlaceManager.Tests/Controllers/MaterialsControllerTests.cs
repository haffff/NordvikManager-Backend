using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData;
using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
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
    public class MaterialsControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly MaterialsController _controller;

        public MaterialsControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, AnyUser());
        }

        private static MaterialsController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var lobbyService = new Mock<ILobbyService>().Object;
            var scopeFactory = new Mock<IServiceScopeFactory>().Object;
            var controller = new MaterialsController(mediatorMock.Object, lobbyService, scopeFactory);
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
        // GetResource
        // =========================================================================

        [Fact]
        public async Task GetResource_ReturnsBadRequest_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetResource(Guid.NewGuid(), null, Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetResource_ReturnsBadRequest_WhenResourceDataIsNull()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(((byte[]?)null, MimeType.JPEG));

            // Act
            var result = await _controller.GetResource(Guid.NewGuid(), null, Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetResource_ReturnsFile_WhenResourceExists()
        {
            // Arrange
            var bytes = new byte[] { 1, 2, 3 };
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((bytes, MimeType.JPEG));

            // Act
            var result = await _controller.GetResource(Guid.NewGuid(), null, Guid.NewGuid());

            // Assert
            Assert.IsType<FileContentResult>(result);
        }

        // =========================================================================
        // GetResourceMetadata
        // =========================================================================

        [Fact]
        public async Task GetResourceMetadata_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var resourceDto = new ResourceDTO { Id = Guid.NewGuid() };
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(resourceDto);

            // Act
            var result = await _controller.GetResourceMetadata(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(resourceDto, ok.Value);
        }

        // =========================================================================
        // GetTemplates
        // =========================================================================

        [Fact]
        public async Task GetTemplates_ReturnsBadRequest_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetTemplates(Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetTemplates_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var cards = new List<CardDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, cards));

            // Act
            var result = await _controller.GetTemplates(Guid.NewGuid());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetTemplatesFull
        // =========================================================================

        [Fact]
        public async Task GetTemplatesFull_ReturnsBadRequest_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetTemplatesFull(Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetTemplatesFull_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var cards = new List<CardDto>();
            _mediator.Setup(m => m.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, cards));

            // Act
            var result = await _controller.GetTemplatesFull(Guid.NewGuid());

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GetCards
        // =========================================================================

        [Fact]
        public async Task GetCards_ReturnsBadRequest_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetCards(Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
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
        // GetCard
        // =========================================================================

        [Fact]
        public async Task GetCard_ReturnsBadRequest_WhenPlayerIsNull()
        {
            // Arrange
            SetupPlayer(_mediator, null);

            // Act
            var result = await _controller.GetCard(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetCard_ReturnsOk_WhenPlayerExists()
        {
            // Arrange
            var card = new CardDto { Id = Guid.NewGuid(), Name = "card" };
            _mediator.Setup(m => m.Send(It.IsAny<GetCardCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(card);

            // Act
            var result = await _controller.GetCard(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(card, ok.Value);
        }

        // =========================================================================
        // GetResources
        // =========================================================================

        [Fact]
        public async Task GetResources_ReturnsOk_WithResourceList()
        {
            // Arrange
            var resources = new List<ResourceDTO>();
            _mediator.Setup(m => m.Send(It.IsAny<GetResourcesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, resources));

            // Act
            var result = await _controller.GetResources(Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(resources, ok.Value);
        }

        // =========================================================================
        // AddResource
        // =========================================================================

        [Fact]
        public async Task AddResource_ReturnsOk_WithNewResourceId()
        {
            // Arrange
            var newId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, (Guid?)newId));
            var request = new AddResourceRequest
            {
                Name = "image",
                MimeType = "image/jpeg",
                Key = null,
                Data = "aGVsbG8=" // plain base64, no data: prefix
            };

            // Act
            var result = await _controller.AddResource(Guid.NewGuid(), request);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // RemoveResource
        // =========================================================================

        [Fact]
        public async Task RemoveResource_ReturnsOk_WithCommandResponse()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.RemoveResource(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(CommandResponse.Ok, ok.Value);
        }

        // =========================================================================
        // CreateResource
        // =========================================================================

        [Fact]
        public async Task CreateResource_ReturnsOk_WithNewResourceId()
        {
            // Arrange
            var newId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, (Guid?)newId));

            // Act
            var result = await _controller.CreateResource(Guid.NewGuid(),
                new CreateResourceRequest { Key = "bg-music", Content = "aGVsbG8=" });

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task CreateResource_ReturnsConflict_WhenKeyAlreadyExists()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.AlreadyExists, (Guid?)null));

            // Act
            var result = await _controller.CreateResource(Guid.NewGuid(),
                new CreateResourceRequest { Key = "bg-music", Content = "aGVsbG8=" });

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task CreateResource_ThrowsPermissionException_WhenPlayerMissingInDb()
        {
            // Arrange — CreateResourceCommandHandler throws PermissionException (403) when
            // the player row backing the request can no longer be found.
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new PermissionException(Permission.Read));

            // Act & Assert
            await Assert.ThrowsAsync<PermissionException>(() =>
                _controller.CreateResource(Guid.NewGuid(), new CreateResourceRequest { Key = "bg-music", Content = "aGVsbG8=" }));
        }

        // =========================================================================
        // UpdateResourceData
        // =========================================================================

        [Fact]
        public async Task UpdateResourceData_ReturnsOk_WhenResourceExists()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, (Guid?)id));

            // Act
            var result = await _controller.UpdateResourceData(Guid.NewGuid(),
                new UpdateResourceDataRequest { Id = id, Content = "aGVsbG8=" });

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task UpdateResourceData_ThrowsResourceNotFoundException_WhenResourceMissing()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new ResourceNotFoundException("Resource", Guid.NewGuid()));

            // Act & Assert
            await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
                _controller.UpdateResourceData(Guid.NewGuid(), new UpdateResourceDataRequest { Id = Guid.NewGuid(), Content = "aGVsbG8=" }));
        }

        [Fact]
        public async Task UpdateResourceData_ThrowsPermissionException_WhenNotOwner()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new PermissionException(Permission.Edit));

            // Act & Assert
            await Assert.ThrowsAsync<PermissionException>(() =>
                _controller.UpdateResourceData(Guid.NewGuid(), new UpdateResourceDataRequest { Id = Guid.NewGuid(), Content = "aGVsbG8=" }));
        }

        [Fact]
        public async Task UpdateResourceData_ThrowsWrongArgumentsException_WhenContentNotBase64()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new WrongArgumentsException(nameof(UpdateResourceDataRequest.Content)));

            // Act & Assert
            await Assert.ThrowsAsync<WrongArgumentsException>(() =>
                _controller.UpdateResourceData(Guid.NewGuid(), new UpdateResourceDataRequest { Id = Guid.NewGuid(), Content = "not-base64!!!" }));
        }

        // =========================================================================
        // DeleteResourceData
        // =========================================================================

        [Fact]
        public async Task DeleteResourceData_ReturnsOk_WhenResourceExists()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<DeleteResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            // Act
            var result = await _controller.DeleteResourceData(Guid.NewGuid(), null, Guid.NewGuid());

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task DeleteResourceData_ThrowsResourceNotFoundException_WhenResourceMissing()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<DeleteResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new ResourceNotFoundException("Resource", Guid.NewGuid()));

            // Act & Assert
            await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
                _controller.DeleteResourceData(Guid.NewGuid(), null, Guid.NewGuid()));
        }

        [Fact]
        public async Task DeleteResourceData_ThrowsPermissionException_WhenNotOwner()
        {
            // Arrange
            _mediator.Setup(m => m.Send(It.IsAny<DeleteResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new PermissionException(Permission.Edit));

            // Act & Assert
            await Assert.ThrowsAsync<PermissionException>(() =>
                _controller.DeleteResourceData(Guid.NewGuid(), null, Guid.NewGuid()));
        }
    }
}
