using DndOnePlaceManager.Application.Commands.Playlist.PausePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.StopPlaylist;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <inheritdoc cref="IPlaybackService"/>
    public class PlaybackService : IPlaybackService
    {
        public async Task<CommandResponse> PlayPlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId)
        {
            // Resume-in-place: if a paused entry already exists for this playlist, unpause it
            // rather than restarting from track 0 / re-shuffling.
            if (lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var existing) && existing.IsPaused)
            {
                existing.IsPaused = false;
                existing.CurrentTrackStartedAtUtc = DateTime.UtcNow;

                await BroadcastPlaylist(lobby, player, WebSocketCommandNames.PlaylistPlay, new
                {
                    playlistId = existing.PlaylistId,
                    mode = existing.Mode,
                    shuffle = existing.Shuffle,
                    repeat = existing.Repeat,
                    trackOrder = existing.TrackOrder,
                    currentTrackIndex = existing.CurrentTrackIndex,
                    currentTrackStartedAtUtc = existing.CurrentTrackStartedAtUtc,
                    isPaused = false,
                });

                return CommandResponse.Ok;
            }

            var result = await mediator.Send(new PlayPlaylistCommand
            {
                GameId = lobby.GameId,
                Player = player,
                PlaylistId = playlistId,
            });

            if (result.Response == CommandResponse.Ok && result.TrackOrder.Count > 0)
            {
                var state = new PlaylistPlaybackState
                {
                    PlaylistId = playlistId,
                    Mode = result.Mode,
                    Shuffle = result.Shuffle,
                    Repeat = result.Repeat,
                    TrackOrder = result.TrackOrder,
                    CurrentTrackIndex = 0,
                    IsPaused = false,
                    CurrentTrackStartedAtUtc = DateTime.UtcNow,
                };
                lobby.ActivePlaylistPlaybacks[playlistId] = state;

                await BroadcastPlaylist(lobby, player, WebSocketCommandNames.PlaylistPlay, new
                {
                    playlistId = state.PlaylistId,
                    mode = state.Mode,
                    shuffle = state.Shuffle,
                    repeat = state.Repeat,
                    trackOrder = state.TrackOrder,
                    currentTrackIndex = state.CurrentTrackIndex,
                    currentTrackStartedAtUtc = state.CurrentTrackStartedAtUtc,
                    isPaused = false,
                });
            }

            return result.Response;
        }

        public async Task<CommandResponse> PausePlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId)
        {
            var result = await mediator.Send(new PausePlaylistCommand
            {
                GameId = lobby.GameId,
                Player = player,
                PlaylistId = playlistId,
            });

            if (result == CommandResponse.Ok &&
                lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var state))
            {
                state.IsPaused = true;
                await BroadcastPlaylist(lobby, player, WebSocketCommandNames.PlaylistPause,
                    new { playlistId });
            }

            return result;
        }

        public async Task<CommandResponse> StopPlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId)
        {
            var result = await mediator.Send(new StopPlaylistCommand
            {
                GameId = lobby.GameId,
                Player = player,
                PlaylistId = playlistId,
            });

            if (result == CommandResponse.Ok)
            {
                lobby.ActivePlaylistPlaybacks.TryRemove(playlistId, out _);
                await BroadcastPlaylist(lobby, player, WebSocketCommandNames.PlaylistStop,
                    new { playlistId });
            }

            return result;
        }

        private static async Task BroadcastPlaylist(GameLobby lobby, PlayerDTO player, string command, object data)
        {
            await lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = command,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(data),
            });
        }
    }
}
