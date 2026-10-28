using NordvikManager.Application.Commands.Soundboard.GetSoundPlayback;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Soundboard.PlaySound;
using NordvikManager.Application.Commands.Soundboard.StopSound;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Controllers
{
    public class SoundboardControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly SoundboardController _controller;

        public SoundboardControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _mediator.Setup(m => m.Send(It.IsAny<GetSoundPlaybackCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new SoundPlayback(1, Loop: false));
            _controller = CreateController(_mediator, AnyUser());
        }

        private static SoundboardController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null, ILobbyService? lobbyService = null)
        {
            var controller = new SoundboardController(mediatorMock.Object, lobbyService ?? new Mock<ILobbyService>().Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetPlayerCommandResponse { Player = player });

        /// <summary>
        /// PlaySound/StopSound broadcast over lobby.HandlePostCommand, which needs a real
        /// GameLobby — not just a mocked ILobbyService. Same construction as
        /// PlaylistControllerTests.MakeLobby().
        /// </summary>
        private static GameLobby MakeLobby(Mock<IMediator> mediatorMock)
        {
            var actionProcessingService = new Mock<IActionProcessingService>();

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(actionProcessingService.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            provider.Setup(p => p.GetService(typeof(IMediator)))
                    .Returns(mediatorMock.Object);

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        private static ILobbyService LobbyServiceReturning(GameLobby lobby)
        {
            var lobbyService = new Mock<ILobbyService>();
            lobbyService.Setup(l => l.GetLobby(It.IsAny<Guid>())).Returns(lobby);
            return lobbyService.Object;
        }

        // =========================================================================
        // PlaySound
        // =========================================================================

        [Fact]
        public async Task PlaySound_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.PlaySound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = Guid.NewGuid() });

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PlaySound_ReturnsResponse_WithoutBroadcasting_WhenPermissionDenied()
        {
            // Permission gating: PlaySoundCommandHandler denies playback for players without
            // access to the resource — the controller must not broadcast sound_play in that case.
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            var listener = new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" };
            lobby.ConnectedPlayers[listener] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));

            _mediator.Setup(m => m.Send(It.IsAny<PlaySoundCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.NoPermission);

            var result = await controller.PlaySound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = Guid.NewGuid() });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
            connection.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task PlaySound_BroadcastsResourceIdNotUnderIdOrParentIdKey_WhenCommandSucceeds()
        {
            // The broadcast payload deliberately never places resourceId under a bare
            // "id"/"parentId" key — GameLobby.HandlePostCommand would otherwise restrict
            // delivery to only players with a Read permission row on that specific resource,
            // breaking "heard by everyone connected to the game."
            var connection = new Mock<IPlayerConnection>();
            WebSocketCommand? sent = null;
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                      .Callback<object>(m => sent = m as WebSocketCommand)
                      .ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            var listener = new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" };
            lobby.ConnectedPlayers[listener] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));

            var resourceId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<PlaySoundCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await controller.PlaySound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = resourceId });

            Assert.IsType<OkObjectResult>(result);
            connection.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Once);
            Assert.NotNull(sent);
            Assert.Equal(WebSocketCommandNames.SoundPlay, sent!.Command);
            Assert.Null(sent.Data[WebSocketCommandNames.DataKeyId]);
            Assert.Null(sent.Data[WebSocketCommandNames.DataKeyParentId]);
            Assert.Equal(resourceId, sent.Data["resourceId"]!.ToObject<Guid>());
        }

        // =========================================================================
        // StopSound
        // =========================================================================

        [Fact]
        public async Task StopSound_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.StopSound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = Guid.NewGuid() });

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task StopSound_BroadcastsResourceId_WhenCommandSucceeds()
        {
            var connection = new Mock<IPlayerConnection>();
            WebSocketCommand? sent = null;
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                      .Callback<object>(m => sent = m as WebSocketCommand)
                      .ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            var listener = new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" };
            lobby.ConnectedPlayers[listener] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));

            var resourceId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<StopSoundCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await controller.StopSound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = resourceId });

            Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(sent);
            Assert.Equal(WebSocketCommandNames.SoundStop, sent!.Command);
            Assert.Equal(resourceId, sent.Data["resourceId"]!.ToObject<Guid>());
        }

        [Fact]
        public async Task StopSound_DoesNotBroadcast_WhenCommandFails()
        {
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            var listener = new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" };
            lobby.ConnectedPlayers[listener] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));

            _mediator.Setup(m => m.Send(It.IsAny<StopSoundCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.NoResource);

            var result = await controller.StopSound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = Guid.NewGuid() });

            Assert.IsType<OkObjectResult>(result);
            connection.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task PlaySound_SendsTheGmsVolume_FileTimesSoundboard()
        {
            var connection = new Mock<IPlayerConnection>();
            WebSocketCommand? sent = null;
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).Callback<object>(m => sent = m as WebSocketCommand).ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" }] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));
            var resourceId = Guid.NewGuid();
            var soundboardId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<PlaySoundCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);
            _mediator.Setup(m => m.Send(It.Is<GetSoundPlaybackCommand>(c => c.ResourceId == resourceId && c.SoundboardId == soundboardId), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new SoundPlayback(0.3, Loop: true));

            await controller.PlaySound(Guid.NewGuid(), new ResourceIdRequest { ResourceId = resourceId, SoundboardId = soundboardId });

            Assert.Equal(0.3, sent!.Data["volume"]!.ToObject<double>());
            Assert.True(sent.Data["loop"]!.ToObject<bool>());
        }

        // =========================================================================
        // StopAllSounds
        // =========================================================================

        [Fact]
        public async Task StopAllSounds_BroadcastsStopAll_WhenAllowed()
        {
            var connection = new Mock<IPlayerConnection>();
            WebSocketCommand? sent = null;
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).Callback<object>(m => sent = m as WebSocketCommand).ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" }] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));
            _mediator.Setup(m => m.Send(It.IsAny<StopSoundCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);

            var result = await controller.StopAllSounds(Guid.NewGuid());

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(WebSocketCommandNames.SoundStopAll, sent!.Command);
        }

        [Fact]
        public async Task StopAllSounds_DoesNotBroadcast_WhenDenied()
        {
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).ReturnsAsync(true);
            var lobby = MakeLobby(_mediator);
            lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" }] = new List<IPlayerConnection> { connection.Object };
            var controller = CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby));
            _mediator.Setup(m => m.Send(It.IsAny<StopSoundCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.NoPermission);

            await controller.StopAllSounds(Guid.NewGuid());

            connection.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Never);
        }
    }
}
