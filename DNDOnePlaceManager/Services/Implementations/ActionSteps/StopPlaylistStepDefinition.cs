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
    public class StopPlaylistStepDefinition : IActionStepDefinition
    {
        private readonly IPlaybackService _playback;

        public StopPlaylistStepDefinition(IPlaybackService playback)
        {
            _playback = playback;
        }

        public string Name => "Stop Playlist";
        public string Value => "StopPlaylist";
        public string Category => "Audio";
        public string Description => "Stops a music playlist for everyone in the game and clears its playback state.";
        public Type DataType => typeof(PlaylistStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<PlaylistStepData>();

            if (!Guid.TryParse(stepData.PlaylistId?.Trim(), out var playlistId))
                throw new ActionProcessException("StopPlaylist: 'PlaylistId' must be a valid playlist ID.");

            await _playback.StopPlaylistAsync(mediator, gameLobby, gameLobby.SystemPlayer, playlistId);
        }
    }
}
