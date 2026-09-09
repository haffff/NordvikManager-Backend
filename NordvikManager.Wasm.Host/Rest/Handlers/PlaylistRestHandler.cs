using DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using DndOnePlaceManager.Application.Commands.Playlist.DeletePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PausePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.StopPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>Ported from DNDOnePlaceManager/Controllers/PlaylistController.cs (write actions).</summary>
    public class PlaylistRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;

        public PlaylistRestHandler(IMediator mediator, ILobbyRegistry lobbyRegistry)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();
            var method = (request.Method ?? string.Empty).ToUpperInvariant();

            switch ($"{method} {path}")
            {
                case "POST api/playlist/addplaylist":
                {
                    var body = request.Body;
                    var (_, id) = await _mediator.Send(new AddPlaylistCommand
                    {
                        GameId = gameId,
                        Player = player,
                        Name = body?["name"]?.Value<string>(),
                        Description = body?["description"]?.Value<string>(),
                        Mode = body?["mode"]?.ToObject<PlaybackMode?>() ?? PlaybackMode.Sequential,
                        Shuffle = body?["shuffle"]?.Value<bool?>() ?? false,
                        Repeat = body?["repeat"]?.Value<bool?>() ?? true,
                        Kind = body?["kind"]?.ToObject<PlaylistKind?>() ?? PlaylistKind.Music,
                        ResourceIds = body?["resourceIds"]?.ToObject<List<Guid>>() ?? new List<Guid>(),
                    });

                    await NotifyPlaylistChange(gameId, player, "add", id);
                    return new WebRTCApiResult(200, new { id });
                }

                case "PUT api/playlist/updateplaylist":
                {
                    var body = request.Body;
                    Guid.TryParse(body?["id"]?.Value<string>(), out var playlistId);

                    var result = await _mediator.Send(new UpdatePlaylistCommand
                    {
                        GameId = gameId,
                        Player = player,
                        PlaylistId = playlistId,
                        Name = body?["name"]?.Value<string>(),
                        Description = body?["description"]?.Value<string>(),
                        Mode = body?["mode"]?.ToObject<PlaybackMode?>() ?? PlaybackMode.Sequential,
                        Shuffle = body?["shuffle"]?.Value<bool?>() ?? false,
                        Repeat = body?["repeat"]?.Value<bool?>() ?? true,
                        Kind = body?["kind"]?.ToObject<PlaylistKind?>() ?? PlaylistKind.Music,
                        ResourceIds = body?["resourceIds"]?.ToObject<List<Guid>>() ?? new List<Guid>(),
                    });

                    await NotifyPlaylistChange(gameId, player, "update", playlistId);
                    return new WebRTCApiResult(200, result);
                }

                case "DELETE api/playlist/removeplaylist":
                {
                    var playlistId = request.Body?.ToObject<Guid>() ?? Guid.Empty;
                    var result = await _mediator.Send(new DeletePlaylistCommand { GameId = gameId, Player = player, PlaylistId = playlistId });
                    await NotifyPlaylistChange(gameId, player, "delete", playlistId);
                    return new WebRTCApiResult(200, result);
                }

                case "POST api/playlist/playplaylist":
                {
                    Guid.TryParse(request.Body?["playlistId"]?.Value<string>(), out var playlistId);

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        return new WebRTCApiResult(400, null);

                    // Resume-in-place: if a paused entry already exists for this playlist, unpause
                    // it rather than restarting from track 0 / re-shuffling — never calls mediator.Send
                    // in this branch, matching the original exactly.
                    if (lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var existing) && existing.IsPaused)
                    {
                        existing.IsPaused = false;
                        existing.CurrentTrackStartedAtUtc = DateTime.UtcNow;

                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistPlay, new
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

                        return new WebRTCApiResult(200, new { response = CommandResponse.Ok });
                    }

                    var result = await _mediator.Send(new PlayPlaylistCommand { GameId = gameId, Player = player, PlaylistId = playlistId });

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

                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistPlay, new
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

                    return new WebRTCApiResult(200, new { response = result.Response });
                }

                case "POST api/playlist/pauseplaylist":
                {
                    Guid.TryParse(request.Body?["playlistId"]?.Value<string>(), out var playlistId);
                    var result = await _mediator.Send(new PausePlaylistCommand { GameId = gameId, Player = player, PlaylistId = playlistId });

                    if (result == CommandResponse.Ok && _lobbyRegistry.Games.TryGetValue(gameId, out var lobby) &&
                        lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var state))
                    {
                        state.IsPaused = true;
                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistPause, new { playlistId });
                    }

                    return new WebRTCApiResult(200, new { response = result });
                }

                case "POST api/playlist/stopplaylist":
                {
                    Guid.TryParse(request.Body?["playlistId"]?.Value<string>(), out var playlistId);
                    var result = await _mediator.Send(new StopPlaylistCommand { GameId = gameId, Player = player, PlaylistId = playlistId });

                    if (result == CommandResponse.Ok && _lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                    {
                        lobby.ActivePlaylistPlaybacks.Remove(playlistId);
                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistStop, new { playlistId });
                    }

                    return new WebRTCApiResult(200, new { response = result });
                }

                case "POST api/playlist/advancetrack":
                {
                    Guid.TryParse(request.Body?["playlistId"]?.Value<string>(), out var playlistId);
                    var fromTrackIndex = request.Body?["fromTrackIndex"]?.Value<int?>();

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby) ||
                        !lobby.ActivePlaylistPlaybacks.TryGetValue(playlistId, out var state))
                        return new WebRTCApiResult(200, new { ended = true });

                    // Idempotency guard: someone else already advanced this playlist — no-op.
                    if (fromTrackIndex.HasValue && fromTrackIndex.Value != state.CurrentTrackIndex)
                        return new WebRTCApiResult(200, new { ended = false, skipped = true });

                    var result = await _mediator.Send(new AdvancePlaylistTrackCommand
                    {
                        GameId = gameId,
                        Player = player,
                        PlaylistId = playlistId,
                        CurrentTrackOrder = state.TrackOrder,
                        CurrentTrackIndex = state.CurrentTrackIndex,
                        Shuffle = state.Shuffle,
                        Repeat = state.Repeat,
                    });

                    if (result.Response != CommandResponse.Ok)
                        return new WebRTCApiResult(200, new { response = result.Response });

                    if (result.Ended)
                    {
                        lobby.ActivePlaylistPlaybacks.Remove(playlistId);
                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistStop, new { playlistId });
                    }
                    else
                    {
                        state.TrackOrder = result.NextTrackOrder;
                        state.CurrentTrackIndex = result.NextTrackIndex;
                        state.CurrentTrackStartedAtUtc = DateTime.UtcNow;

                        await BroadcastPlaylist(gameId, player, WebSocketCommandNames.PlaylistTrackChange, new
                        {
                            playlistId,
                            trackId = result.NextTrackId,
                            trackIndex = state.CurrentTrackIndex,
                            trackOrder = state.TrackOrder,
                            currentTrackStartedAtUtc = state.CurrentTrackStartedAtUtc,
                        });
                    }

                    return new WebRTCApiResult(200, new { ended = result.Ended });
                }

                default:
                    return null;
            }
        }

        private async Task BroadcastPlaylist(Guid gameId, PlayerDTO player, string command, object data)
        {
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            await lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = command,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(data),
            });
        }

        private async Task NotifyPlaylistChange(Guid gameId, PlayerDTO player, string action, Guid id)
        {
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            await lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = WebSocketCommandNames.PlaylistNotify,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(new { action, id, gameId }),
            });
        }
    }
}
