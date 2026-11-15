using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Playlist.AddPlaylist;
using NordvikManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using NordvikManager.Application.Commands.Playlist.DeletePlaylist;
using NordvikManager.Application.Commands.Playlist.GetPlaylists;
using NordvikManager.Application.Commands.Playlist.PausePlaylist;
using NordvikManager.Application.Commands.Playlist.PlayPlaylist;
using NordvikManager.Application.Commands.Playlist.StopPlaylist;
using NordvikManager.Application.Commands.Playlist.UpdatePlaylist;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Implementations;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using NordvikManager.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
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

        private static PlaylistController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null, ILobbyService? lobbyService = null)
        {
            // Real PlaybackService (dependency-free) so these tests still exercise the
            // playback orchestration end-to-end after it moved out of the controller.
            var controller = new PlaylistController(
                mediatorMock.Object,
                lobbyService ?? new Mock<ILobbyService>().Object,
                new PlaybackService());
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static ILobbyService LobbyServiceReturning(GameLobby lobby)
        {
            var lobbyService = new Mock<ILobbyService>();
            lobbyService.Setup(l => l.GetLobby(It.IsAny<Guid>())).Returns(lobby);
            return lobbyService.Object;
        }

        // A controller whose game has a real lobby: playback state lives there.
        private (GameLobby Lobby, PlaylistController Controller) WithLobby()
        {
            var lobby = TestLobby.Create(mediator: _mediator.Object);
            return (lobby, CreateController(_mediator, AnyUser(), LobbyServiceReturning(lobby)));
        }

        // A player connected to the lobby; the list fills with what they're sent.
        private static List<WebSocketCommand> Listener(GameLobby lobby)
        {
            var sent = new List<WebSocketCommand>();
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>())).Callback<object>(m => { if (m is WebSocketCommand c) sent.Add(c); }).ReturnsAsync(true);
            lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Listener" }] = new List<IPlayerConnection> { connection.Object };
            return sent;
        }

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

        [Fact]
        public async Task AddPlaylist_IntoAFolder_PassesTheFolder()
        {
            var folder = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<AddPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            await _controller.AddPlaylist(Guid.NewGuid(), new AddPlaylistRequest { Name = "Battle Music", TreeParentId = folder });

            _mediator.Verify(m => m.Send(It.Is<AddPlaylistCommand>(c => c.TreeParentId == folder), It.IsAny<CancellationToken>()), Times.Once);
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

        // =========================================================================
        // PlayPlaylist
        // =========================================================================

        [Fact]
        public async Task PlayPlaylist_CreatesActivePlaybackState_WhenNoneExists()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            var trackOrder = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            _mediator.Setup(m => m.Send(It.Is<PlayPlaylistCommand>(c => c.PlaylistId == playlistId), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PlayPlaylistResult { Response = CommandResponse.Ok, TrackOrder = trackOrder, Mode = PlaybackMode.Sequential });

            var result = await controller.PlayPlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId });

            Assert.IsType<OkObjectResult>(result);
            Assert.True(lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var state));
            Assert.Equal(trackOrder, state!.TrackOrder);
            Assert.False(state.IsPaused);
        }

        [Fact]
        public async Task PlayPlaylist_ResumesInPlace_WithoutReinvokingMediator_WhenPausedEntryExists()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState
            {
                PlaylistId = playlistId,
                IsPaused = true,
                TrackOrder = new List<Guid> { Guid.NewGuid() },
            };

            var result = await controller.PlayPlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId });

            Assert.IsType<OkObjectResult>(result);
            Assert.False(lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
            _mediator.Verify(m => m.Send(It.IsAny<PlayPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // =========================================================================
        // PausePlaylist
        // =========================================================================

        [Fact]
        public async Task PausePlaylist_RunsCommandAndReturnsOk_WhenLobbyMissing()
        {
            // Lenient legacy behaviour: no in-memory lobby, but the permission-checking
            // command still runs and the endpoint returns Ok rather than 400.
            _mediator.Setup(m => m.Send(It.IsAny<PausePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await _controller.PausePlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = Guid.NewGuid() });

            Assert.IsType<OkObjectResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<PausePlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PausePlaylist_SetsIsPaused_WhenCommandSucceeds()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId };
            _mediator.Setup(m => m.Send(It.IsAny<PausePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await controller.PausePlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId });

            Assert.IsType<OkObjectResult>(result);
            Assert.True(lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
        }

        // =========================================================================
        // StopPlaylist
        // =========================================================================

        [Fact]
        public async Task StopPlaylist_RunsCommandAndReturnsOk_WhenLobbyMissing()
        {
            _mediator.Setup(m => m.Send(It.IsAny<StopPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await _controller.StopPlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = Guid.NewGuid() });

            Assert.IsType<OkObjectResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<StopPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopPlaylist_RemovesActiveEntry_WhenCommandSucceeds()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId };
            _mediator.Setup(m => m.Send(It.IsAny<StopPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var result = await controller.StopPlaylist(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId });

            Assert.IsType<OkObjectResult>(result);
            Assert.False(lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        // =========================================================================
        // AdvanceTrack
        // =========================================================================

        [Fact]
        public async Task AdvanceTrack_SkipsWithoutCallingMediator_WhenFromTrackIndexIsStale()
        {
            // Idempotency guard: a second caller (e.g. a duplicate GM tab) racing to advance
            // the same playlist should no-op instead of double-skipping a track.
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, CurrentTrackIndex = 2 };

            var result = await controller.AdvanceTrack(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId, FromTrackIndex = 0 });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
            _mediator.Verify(m => m.Send(It.IsAny<AdvancePlaylistTrackCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AdvanceTrack_ReturnsEnded_WithoutThrowing_WhenNoActiveEntry()
        {
            var (lobby, controller) = WithLobby();

            var result = await controller.AdvanceTrack(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = Guid.NewGuid() });

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task AdvanceTrack_RemovesActiveEntry_WhenPlaylistEnded()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, CurrentTrackIndex = 0 };
            _mediator.Setup(m => m.Send(It.IsAny<AdvancePlaylistTrackCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AdvancePlaylistTrackResult { Response = CommandResponse.Ok, Ended = true });

            var result = await controller.AdvanceTrack(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId, FromTrackIndex = 0 });

            Assert.IsType<OkObjectResult>(result);
            Assert.False(lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        [Fact]
        public async Task AdvanceTrack_UpdatesActiveEntry_WhenPlaylistContinues()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            var nextOrder = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, CurrentTrackIndex = 0 };
            _mediator.Setup(m => m.Send(It.IsAny<AdvancePlaylistTrackCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AdvancePlaylistTrackResult { Response = CommandResponse.Ok, Ended = false, NextTrackOrder = nextOrder, NextTrackIndex = 1, NextTrackId = nextOrder[1] });

            var result = await controller.AdvanceTrack(Guid.NewGuid(), new PlaylistIdRequest { PlaylistId = playlistId, FromTrackIndex = 0 });

            Assert.IsType<OkObjectResult>(result);
            var state = lobby.ActivePlaylistPlaybacks[playlistId];
            Assert.Equal(1, state.CurrentTrackIndex);
            Assert.Equal(nextOrder, state.TrackOrder);
        }

        // =========================================================================
        // GetCurrentPlayback
        // =========================================================================

        [Fact]
        public async Task GetCurrentPlayback_ReturnsEmptyList_WhenLobbyMissing()
        {
            var result = await _controller.GetCurrentPlayback(Guid.NewGuid());

            var ok = Assert.IsType<OkObjectResult>(result);
            var data = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);
            Assert.Empty(data.Cast<object>());
        }

        [Fact]
        public async Task GetCurrentPlayback_ReturnsActiveStates_WhenLobbyHasEntries()
        {
            var (lobby, controller) = WithLobby();
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, IsPaused = true };

            var result = await controller.GetCurrentPlayback(Guid.NewGuid());

            var ok = Assert.IsType<OkObjectResult>(result);
            var data = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value).Cast<object>().ToList();
            Assert.Single(data);
        }

        // =========================================================================
        // The GM's playlist volume
        // =========================================================================

        [Fact]
        public async Task UpdatePlaylist_PassesTheVolume_AndTellsEveryoneWhileItPlays()
        {
            var (lobby, controller) = WithLobby();
            var sent = Listener(lobby);
            var playlistId = Guid.NewGuid();
            lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, Volume = 1 };
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePlaylistCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);

            await controller.UpdatePlaylist(Guid.NewGuid(), new UpdatePlaylistRequest { Id = playlistId, Name = "Tavern", Description = "", Volume = 0.4 });

            _mediator.Verify(m => m.Send(It.Is<UpdatePlaylistCommand>(c => c.Volume == 0.4), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(0.4, lobby.ActivePlaylistPlaybacks[playlistId].Volume);
            var volume = Assert.Single(sent, c => c.Command == WebSocketCommandNames.PlaylistVolume);
            Assert.Equal((playlistId, 0.4), (volume.Data["playlistId"]!.ToObject<Guid>(), volume.Data["volume"]!.ToObject<double>()));
        }

        [Fact]
        public async Task UpdatePlaylist_NotPlaying_SendsNoVolumeChange()
        {
            var (lobby, controller) = WithLobby();
            var sent = Listener(lobby);
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePlaylistCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok);

            await controller.UpdatePlaylist(Guid.NewGuid(), new UpdatePlaylistRequest { Id = Guid.NewGuid(), Name = "Tavern", Description = "", Volume = 0.4 });

            Assert.DoesNotContain(sent, c => c.Command == WebSocketCommandNames.PlaylistVolume);
        }
    }
}
