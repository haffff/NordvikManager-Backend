using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the Play / Pause / Stop Playlist action steps, which delegate to
    // IPlaybackService so an action can drive music the same way the Playlists panel does.
    public class PlaylistStepDefinitionsTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IPlaybackService> _playback = new();

        private readonly PlayPlaylistStepDefinition _play;
        private readonly PausePlaylistStepDefinition _pause;
        private readonly StopPlaylistStepDefinition _stop;

        public PlaylistStepDefinitionsTests()
        {
            _play = new PlayPlaylistStepDefinition(_playback.Object);
            _pause = new PausePlaylistStepDefinition(_playback.Object);
            _stop = new StopPlaylistStepDefinition(_playback.Object);
        }

        private static ActionStep MakeStep(string type, string playlistId) =>
            new ActionStep { Type = type, Data = JObject.FromObject(new { PlaylistId = playlistId }) };

        private static GameLobby MakeLobby()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

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

        [Theory]
        [InlineData("")]
        [InlineData("nope")]
        [InlineData(null)]
        public async Task Play_InvalidPlaylistId_Throws(string? playlistId)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _play.Execute(_mediator.Object, new(), MakeLobby(), MakeStep("PlayPlaylist", playlistId)));
        }

        [Fact]
        public async Task Play_ValidId_DelegatesToPlaybackServiceWithSystemPlayer()
        {
            var lobby = MakeLobby();
            var playlistId = Guid.NewGuid();
            _playback.Setup(p => p.PlayPlaylistAsync(_mediator.Object, lobby, lobby.SystemPlayer, playlistId))
                     .ReturnsAsync(CommandResponse.Ok);

            await _play.Execute(_mediator.Object, new(), lobby, MakeStep("PlayPlaylist", playlistId.ToString()));

            _playback.Verify(p => p.PlayPlaylistAsync(_mediator.Object, lobby, lobby.SystemPlayer, playlistId), Times.Once);
        }

        [Fact]
        public async Task Play_PlaybackReturnsNoResource_Throws()
        {
            var lobby = MakeLobby();
            _playback.Setup(p => p.PlayPlaylistAsync(It.IsAny<IMediator>(), It.IsAny<GameLobby>(), It.IsAny<PlayerDTO>(), It.IsAny<Guid>()))
                     .ReturnsAsync(CommandResponse.NoResource);

            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _play.Execute(_mediator.Object, new(), lobby, MakeStep("PlayPlaylist", Guid.NewGuid().ToString())));
        }

        [Fact]
        public async Task Pause_ValidId_Delegates()
        {
            var lobby = MakeLobby();
            var playlistId = Guid.NewGuid();
            _playback.Setup(p => p.PausePlaylistAsync(It.IsAny<IMediator>(), It.IsAny<GameLobby>(), It.IsAny<PlayerDTO>(), It.IsAny<Guid>()))
                     .ReturnsAsync(CommandResponse.NoChange);

            await _pause.Execute(_mediator.Object, new(), lobby, MakeStep("PausePlaylist", playlistId.ToString()));

            _playback.Verify(p => p.PausePlaylistAsync(_mediator.Object, lobby, lobby.SystemPlayer, playlistId), Times.Once);
        }

        [Fact]
        public async Task Stop_ValidId_Delegates()
        {
            var lobby = MakeLobby();
            var playlistId = Guid.NewGuid();
            _playback.Setup(p => p.StopPlaylistAsync(It.IsAny<IMediator>(), It.IsAny<GameLobby>(), It.IsAny<PlayerDTO>(), It.IsAny<Guid>()))
                     .ReturnsAsync(CommandResponse.Ok);

            await _stop.Execute(_mediator.Object, new(), lobby, MakeStep("StopPlaylist", playlistId.ToString()));

            _playback.Verify(p => p.StopPlaylistAsync(_mediator.Object, lobby, lobby.SystemPlayer, playlistId), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("nope")]
        public async Task PauseAndStop_InvalidPlaylistId_Throw(string playlistId)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _pause.Execute(_mediator.Object, new(), MakeLobby(), MakeStep("PausePlaylist", playlistId)));
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                _stop.Execute(_mediator.Object, new(), MakeLobby(), MakeStep("StopPlaylist", playlistId)));
        }

        [Fact]
        public void Definitions_ExposeAudioCategoryMetadata()
        {
            Assert.Equal("PlayPlaylist", _play.Value);
            Assert.Equal("PausePlaylist", _pause.Value);
            Assert.Equal("StopPlaylist", _stop.Value);
            Assert.Equal("Audio", _play.Category);
            Assert.Equal("Audio", _pause.Category);
            Assert.Equal("Audio", _stop.Category);
            Assert.Equal(typeof(PlaylistStepData), _play.DataType);
            Assert.Equal(typeof(PlaylistStepData), _pause.DataType);
            Assert.Equal(typeof(PlaylistStepData), _stop.DataType);
        }
    }
}
