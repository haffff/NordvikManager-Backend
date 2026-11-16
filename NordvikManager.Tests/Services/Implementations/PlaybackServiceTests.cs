using NordvikManager.Application.Commands.Playlist.PausePlaylist;
using NordvikManager.Application.Commands.Playlist.PlayPlaylist;
using NordvikManager.Application.Commands.Playlist.StopPlaylist;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
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
            _lobby = TestLobby.Create(mediator: _mediator.Object);
        }

        private void Answer<TCommand, TResult>(TResult value) where TCommand : IRequest<TResult>
            => _mediator.Setup(m => m.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(value);

        // A playlist that is already playing (or paused) in the lobby.
        private Guid Playing(bool paused = false, params Guid[] tracks)
        {
            var playlistId = Guid.NewGuid();
            _lobby.ActivePlaylistPlaybacks[playlistId] = new PlaylistPlaybackState { PlaylistId = playlistId, IsPaused = paused, TrackOrder = tracks.ToList() };
            return playlistId;
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
            var playlistId = Playing(paused: true, Guid.NewGuid());

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
            _mediator.Verify(m => m.Send(It.IsAny<PlayPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlayPlaylistAsync_DoesNotCreateState_WhenResultIsNoResource()
        {
            var playlistId = Guid.NewGuid();
            Answer<PlayPlaylistCommand, PlayPlaylistResult>(new PlayPlaylistResult { Response = CommandResponse.NoResource });

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.NoResource, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        [Fact]
        public async Task PlayPlaylistAsync_DoesNotCreateState_WhenTrackOrderEmpty()
        {
            var playlistId = Guid.NewGuid();
            Answer<PlayPlaylistCommand, PlayPlaylistResult>(new PlayPlaylistResult { Response = CommandResponse.Ok, TrackOrder = new List<Guid>() });

            var response = await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        // ── PausePlaylist ───────────────────────────────────────────────────────

        [Fact]
        public async Task PausePlaylistAsync_SetsIsPaused_WhenOkAndEntryExists()
        {
            var playlistId = Playing();
            Answer<PausePlaylistCommand, CommandResponse>(CommandResponse.Ok);

            var response = await _service.PausePlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(_lobby.ActivePlaylistPlaybacks[playlistId].IsPaused);
        }

        [Fact]
        public async Task PausePlaylistAsync_ReturnsResponse_WhenNoActiveEntry()
        {
            Answer<PausePlaylistCommand, CommandResponse>(CommandResponse.Ok);

            var response = await _service.PausePlaylistAsync(_mediator.Object, _lobby, _player, Guid.NewGuid());

            Assert.Equal(CommandResponse.Ok, response);
        }

        // ── StopPlaylist ────────────────────────────────────────────────────────

        [Fact]
        public async Task StopPlaylistAsync_RemovesEntry_WhenOk()
        {
            var playlistId = Playing();
            Answer<StopPlaylistCommand, CommandResponse>(CommandResponse.Ok);

            var response = await _service.StopPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        [Fact]
        public async Task StopPlaylistAsync_KeepsEntry_WhenCommandNotOk()
        {
            var playlistId = Playing();
            Answer<StopPlaylistCommand, CommandResponse>(CommandResponse.NoPermission);

            var response = await _service.StopPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            Assert.Equal(CommandResponse.NoPermission, response);
            Assert.True(_lobby.ActivePlaylistPlaybacks.ContainsKey(playlistId));
        }

        // ── The GM's volumes ────────────────────────────────────────────────────

        private sealed class RecordingConnection : NordvikManager.WebRTC.IPlayerConnection
        {
            public List<NordvikManager.WebSockets.WebSocketCommand> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message)
            {
                if (message is NordvikManager.WebSockets.WebSocketCommand c) Sent.Add(c);
                return Task.FromResult(true);
            }
        }

        [Fact]
        public async Task PlayPlaylistAsync_KeepsTheVolumes_AndSendsThemToEveryone()
        {
            var playlistId = Guid.NewGuid();
            var loudTrack = Guid.NewGuid();
            var everyone = new RecordingConnection();
            _lobby.ConnectedPlayers[new PlayerDTO { Id = Guid.NewGuid(), Name = "Someone" }] = new List<NordvikManager.WebRTC.IPlayerConnection> { everyone };
            Answer<PlayPlaylistCommand, PlayPlaylistResult>(new PlayPlaylistResult
            {
                Response = CommandResponse.Ok, TrackOrder = new List<Guid> { loudTrack }, Volume = 0.8,
                TrackVolumes = new Dictionary<Guid, double> { [loudTrack] = 0.5 },
            });

            await _service.PlayPlaylistAsync(_mediator.Object, _lobby, _player, playlistId);

            var state = _lobby.ActivePlaylistPlaybacks[playlistId];
            Assert.Equal(0.8, state.Volume);
            Assert.Equal(0.5, state.TrackVolumes[loudTrack]);
            var sent = Assert.Single(everyone.Sent);
            Assert.Equal(0.8, sent.Data["volume"]!.ToObject<double>());
            Assert.Equal(0.5, sent.Data["trackVolumes"]![loudTrack.ToString()]!.ToObject<double>());
        }
    }
}
