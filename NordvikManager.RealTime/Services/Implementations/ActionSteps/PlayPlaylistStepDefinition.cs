using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using NordvikManager.Services.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    public class PlayPlaylistStepDefinition : IActionStepDefinition
    {
        private readonly IPlaybackService _playback;

        public PlayPlaylistStepDefinition(IPlaybackService playback)
        {
            _playback = playback;
        }

        public string Name => "Play Playlist";
        public string Value => "PlayPlaylist";
        public IReadOnlyList<string> Keywords => new[] { "music", "playlist", "audio" };
        public string Category => "Audio";
        public string Description => "Starts (or resumes, if paused) a music playlist for everyone in the game.";
        public string? Summary => "Play playlist {PlaylistId}";
        public Type DataType => typeof(PlaylistStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<PlaylistStepData>();

            if (!Guid.TryParse(stepData.PlaylistId?.Trim(), out var playlistId))
                throw new ActionProcessException("PlayPlaylist: 'PlaylistId' must be a valid playlist ID.");

            var response = await _playback.PlayPlaylistAsync(mediator, gameLobby, gameLobby.SystemPlayer, playlistId);

            if (response == CommandResponse.NoResource)
                throw new ActionProcessException($"PlayPlaylist: playlist '{playlistId}' was not found or has no tracks.");
        }
    }
}
