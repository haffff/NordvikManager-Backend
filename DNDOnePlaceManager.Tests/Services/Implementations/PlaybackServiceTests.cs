using DndOnePlaceManager.Application.Commands.Playlist.PausePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.StopPlaylist;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations
{
    // Covers PlaybackService — the playlist playback orchestration extracted from
    // PlaylistController so the HTTP endpoints and the Play/Pause/Stop Playlist action
    // steps share one implementation of the in-memory state + playlist_* broadcasts.
    public class PlaybackServiceTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly IPlaybackService _service = new PlaybackService();
        private readonly GameLobby _lobby;
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Player" };

        public PlaybackServiceTests()
        {
            _lobby = MakeLobby(_mediator);
        }

        // Same construction as PlaylistControllerTests.MakeLobby — HandlePostCommand
        // resolves its own scoped IMediator internally, so the provider must supply one.
        private static GameLobby MakeLobby(Mock<IMediator> mediatorMock)
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            provider.Setup(p => p.GetService(typeof(IMediator))).Returns(mediatorMock.Object);

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

        // ── PlayPlaylist ────────────────────────────────────────────────────────

        [Fact]
        public async Task PlayPlaylistAsync_CreatesActiveState_WhenNoneExists()
        {
            var playlistId = Guid.NewGuid();
            var order = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            _mediator.Setup(m => m.Send(It.Is<PlayPlaylistCommand>(c => c.PlaylistId == playlistId && c.GameId == _lobby.GameId),
                                       It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PlayPlaylistResult { Response = CommandResponse.Ok, TrackOrder = order, Mode = PlaybackMode.Sequential });

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(_lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var state));
            Assert.Equal(order, state!.TrackOrder);
            Assert.False(state.IsPaused);
            Assert.Equal(0, state.CurrentTrackIndex);
        }

        [Fact]
        public async Task PlayPlaylistAsync_ResumesInPlace_WithoutCallingMediator_WhenPausedEntryExists()
        {
            var playlistId = Guid.NewGuid();
            _lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState
            {
                PlaylistId = playlistId,
                IsPaused = true,
                TrackOrder = new List<Guid> { Guid.NewGuid() },
            };

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
            _mediator.Verify(m => m.Send(It.IsAny<PlayPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlayPlaylistAsync_DoesNotCreateState_WhenResultIsNoResource()
        {
            var playlistId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<PlayPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PlayPlaylistResult { Response = CommandResponse.NoResource });

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.NoResource, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        [Fact]
        public async Task PlayPlaylistAsync_DoesNotCreateState_WhenTrackOrderEmpty()
        {
            var playlistId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<PlayPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PlayPlaylistResult { Response = CommandResponse.Ok, TrackOrder = new List<Guid>() });

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        // ── PausePlaylist ───────────────────────────────────────────────────────

        [Fact]
        public async Task PausePlaylistAsync_SetsIsPaused_WhenOkAndEntryExists()
        {
            var playlistId = Guid.NewGuid();
            _lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId };
            _mediator.Setup(m => m.Send(It.IsAny<PausePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var response = await _service.PausePlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(_lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
        }

        [Fact]
        public async Task PausePlaylistAsync_ReturnsResponse_WhenNoActiveEntry()
        {
            _mediator.Setup(m => m.Send(It.IsAny<PausePlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var response = await _service.PausePlaylistAsync(_mediator.Object, _lobby, _player, Guid.NewGuid());

            Assert.Equal(CommandResponse.Ok, response);
        }

        // ── StopPlaylist ────────────────────────────────────────────────────────

        [Fact]
        public async Task StopPlaylistAsync_RemovesEntry_WhenOk()
        {
            var playlistId = Guid.NewGuid();
            _lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId };
            _mediator.Setup(m => m.Send(It.IsAny<StopPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);

            var response = await _service.StopPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        [Fact]
        public async Task StopPlaylistAsync_KeepsEntry_WhenCommandNotOk()
        {
            var playlistId = Guid.NewGuid();
            _lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId };
            _mediator.Setup(m => m.Send(It.IsAny<StopPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.NoPermission);

            var response = await _service.StopPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.NoPermission, response);
            Assert.True(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }
    }
}
