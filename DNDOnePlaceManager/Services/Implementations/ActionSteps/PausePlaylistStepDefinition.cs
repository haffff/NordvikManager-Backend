using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class PausePlaylistStepDefinition : IActionStepDefinition
    {
        private readonly IPlaybackService _playback;

        public PausePlaylistStepDefinition(IPlaybackService playback)
        {
            _playback = playback;
        }

        public string Name => "Pause Playlist";
        public string Value => "PausePlaylist";
        public string Category => "Audio";
        public string Description => "Pauses a currently playing music playlist for everyone in the game.";
        public Type DataType => typeof(PlaylistStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<PlaylistStepData>();

            if (!Guid.TryParse(stepData.PlaylistId?.Trim(), out var playlistId))
                throw new ActionProcessException("PausePlaylist: 'PlaylistId' must be a valid playlist ID.");

            await _playback.PausePlaylistAsync(mediator, gameLobby, gameLobby.SystemPlayer, playlistId);
        }
    }
}
