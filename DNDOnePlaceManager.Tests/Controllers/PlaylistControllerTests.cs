using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.DeletePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists;
using DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class PlaylistControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlaylistController _controller;

        public PlaylistControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, AnyUser());
        }

        private static PlaylistController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var lobbyService = new Mock<ILobbyService>().Object;
            var controller = new PlaylistController(mediatorMock.Object, lobbyService);
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
        // GetPlaylists
        // =========================================================================

        [Fact]
        public async Task GetPlaylists_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.GetPlaylists(Guid.NewGuid());

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetPlaylists_ReturnsOk_WithPlaylistList()
        {
            var playlists = new List<PlaylistDTO> { new PlaylistDTO { Id = Guid.NewGuid(), Name = "Battle Music" } };
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(playlists);

            var result = await _controller.GetPlaylists(Guid.NewGuid());

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(playlists, ok.Value);
        }

        // =========================================================================
        // AddPlaylist
        // =========================================================================

        [Fact]
        public async Task AddPlaylist_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.AddPlaylist(Guid.NewGuid(), new AddPlaylistRequest { Name = "Battle Music", Description = "Loud" });

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task AddPlaylist_ReturnsOk_WithNewPlaylistId()
        {
            var newId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<AddPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, newId));

            var result = await _controller.AddPlaylist(Guid.NewGuid(), new AddPlaylistRequest { Name = "Battle Music", Description = "Loud" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // UpdatePlaylist
        // =========================================================================

        [Fact]
        public async Task UpdatePlaylist_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.UpdatePlaylist(Guid.NewGuid(), new UpdatePlaylistRequest { Id = Guid.NewGuid(), Name = "New", Description = "New desc" });

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task UpdatePlaylist_ReturnsOk_WithCommandResponse()
        {
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await _controller.UpdatePlaylist(Guid.NewGuid(), new UpdatePlaylistRequest { Id = Guid.NewGuid(), Name = "New", Description = "New desc" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(CommandResponse.Ok, ok.Value);
        }

        // =========================================================================
        // RemovePlaylist
        // =========================================================================

        [Fact]
        public async Task RemovePlaylist_ReturnsBadRequest_WhenPlayerIsNull()
        {
            SetupPlayer(_mediator, null);

            var result = await _controller.RemovePlaylist(Guid.NewGuid(), Guid.NewGuid());

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task RemovePlaylist_ReturnsOk_WithCommandResponse()
        {
            _mediator.Setup(m => m.Send(It.IsAny<DeletePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await _controller.RemovePlaylist(Guid.NewGuid(), Guid.NewGuid());

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(CommandResponse.Ok, ok.Value);
        }
    }
}
