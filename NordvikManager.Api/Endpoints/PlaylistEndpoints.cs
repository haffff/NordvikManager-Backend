using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Playlist.AddPlaylist;
using NordvikManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using NordvikManager.Application.Commands.Playlist.DeletePlaylist;
using NordvikManager.Application.Commands.Playlist.GetPlaylists;
using NordvikManager.Application.Commands.Playlist.PausePlaylist;
using NordvikManager.Application.Commands.Playlist.StopPlaylist;
using NordvikManager.Application.Commands.Playlist.UpdatePlaylist;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Playlist (PlaylistController delegates here).</summary>
    public sealed class PlaylistEndpoints
    {
        private readonly IMediator mediator;
        private readonly ILobbyService _lobbyService;
        private readonly IPlaybackService _playbackService;

        public PlaylistEndpoints(IMediator mediator, ILobbyService lobbyService, IPlaybackService playbackService)
        {
            this.mediator = mediator;
            _lobbyService = lobbyService;
            _playbackService = playbackService;
        }

        [ApiRoute("GET", "api/Playlist/GetPlaylists")]
        public async Task<ApiResult> GetPlaylists(User? user, Guid gameId, PlaylistKind kind = PlaylistKind.Music)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new GetPlaylistsCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Kind = kind,
                IsLocalAdmin = user?.IsLocalAdmin == true,
            });

            return ApiResult.Ok(result);
        }

        [ApiRoute("POST", "api/Playlist/AddPlaylist")]
        public async Task<ApiResult> AddPlaylist(User? user, Guid gameId, AddPlaylistRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var (_, id) = await mediator.Send(new AddPlaylistCommand
            {
                GameId      = gameId,
                Player      = playerResult.Player,
                Name        = request.Name,
                Description = request.Description,
                Mode        = request.Mode,
                Shuffle     = request.Shuffle,
                Repeat      = request.Repeat,
                Kind        = request.Kind,
                ResourceIds = request.ResourceIds,
                Volume      = request.Volume,
                TreeParentId = request.TreeParentId,
            });

            await NotifyPlaylistChange(gameId, playerResult.Player, "add", id);

            return ApiResult.Ok(new { id });
        }

        [ApiRoute("PUT", "api/Playlist/UpdatePlaylist")]
        public async Task<ApiResult> UpdatePlaylist(User? user, Guid gameId, UpdatePlaylistRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new UpdatePlaylistCommand
            {
                GameId      = gameId,
                Player      = playerResult.Player,
                PlaylistId  = request.Id,
                Name        = request.Name,
                Description = request.Description,
                Mode        = request.Mode,
                Shuffle     = request.Shuffle,
                Repeat      = request.Repeat,
                Kind        = request.Kind,
                ResourceIds = request.ResourceIds,
                Volume      = request.Volume,
            });

            // Playing now: everyone hears the new volume straight away.
            if (result == CommandResponse.Ok && request.Volume.HasValue
                && _lobbyService.GetLobby(gameId)?.ActivePlaylistPlaybacks.TryGetValue(request.Id, out var playing) == true)
            {
                playing.Volume = Math.Clamp(request.Volume.Value, 0, 1);
                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistVolume,
                    new { playlistId = request.Id, volume = playing.Volume });
            }

            await NotifyPlaylistChange(gameId, playerResult.Player, "update", request.Id);

            return ApiResult.Ok(result);
        }

        [ApiRoute("DELETE", "api/Playlist/RemovePlaylist")]
        public async Task<ApiResult> RemovePlaylist(User? user, Guid gameId, [ApiBody] Guid playlistId)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new DeletePlaylistCommand
            {
                GameId     = gameId,
                Player     = playerResult.Player,
                PlaylistId = playlistId,
            });

            await NotifyPlaylistChange(gameId, playerResult.Player, "delete", playlistId);

            return ApiResult.Ok(result);
        }

        [ApiRoute("POST", "api/Playlist/PlayPlaylist")]
        public async Task<ApiResult> PlayPlaylist(User? user, Guid gameId, PlaylistIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return ApiResult.BadRequest();

            var response = await _playbackService.PlayPlaylistAsync(mediator, lobby, playerResult.Player, request.PlaylistId);
            return ApiResult.Ok(new { response });
        }

        [ApiRoute("POST", "api/Playlist/PausePlaylist")]
        public async Task<ApiResult> PausePlaylist(User? user, Guid gameId, PlaylistIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
            {
                // No in-memory playback to touch — still run the command for its
                // permission check, matching the pre-refactor lenient behaviour.
                var r = await mediator.Send(new PausePlaylistCommand
                {
                    GameId = gameId,
                    Player = playerResult.Player,
                    PlaylistId = request.PlaylistId,
                });
                return ApiResult.Ok(new { response = r });
            }

            var response = await _playbackService.PausePlaylistAsync(mediator, lobby, playerResult.Player, request.PlaylistId);
            return ApiResult.Ok(new { response });
        }

        [ApiRoute("POST", "api/Playlist/StopPlaylist")]
        public async Task<ApiResult> StopPlaylist(User? user, Guid gameId, PlaylistIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
            {
                // No in-memory playback to touch — still run the command for its
                // permission check, matching the pre-refactor lenient behaviour.
                var r = await mediator.Send(new StopPlaylistCommand
                {
                    GameId = gameId,
                    Player = playerResult.Player,
                    PlaylistId = request.PlaylistId,
                });
                return ApiResult.Ok(new { response = r });
            }

            var response = await _playbackService.StopPlaylistAsync(mediator, lobby, playerResult.Player, request.PlaylistId);
            return ApiResult.Ok(new { response });
        }

        [ApiRoute("POST", "api/Playlist/AdvanceTrack")]
        public async Task<ApiResult> AdvanceTrack(User? user, Guid gameId, PlaylistIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null || !lobby.ActivePlaylistPlaybacks.TryGetValue(request.PlaylistId, out var state))
                return ApiResult.Ok(new { ended = true });

            // Idempotency guard: if the caller's assumed current-track-index no longer
            // matches the authoritative in-memory state, someone else already advanced
            // this playlist (e.g. a second GM tab reacting to the same track ending) —
            // no-op rather than skip a track ahead.
            if (request.FromTrackIndex.HasValue && request.FromTrackIndex.Value != state.CurrentTrackIndex)
                return ApiResult.Ok(new { ended = false, skipped = true });

            var result = await mediator.Send(new AdvancePlaylistTrackCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                PlaylistId = request.PlaylistId,
                CurrentTrackOrder = state.TrackOrder,
                CurrentTrackIndex = state.CurrentTrackIndex,
                Shuffle = state.Shuffle,
                Repeat = state.Repeat,
            });

            if (result.Response != CommandResponse.Ok)
                return ApiResult.Ok(new { response = result.Response });

            if (result.Ended)
            {
                lobby.ActivePlaylistPlaybacks.TryRemove(request.PlaylistId, out _);
                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistStop,
                    new { playlistId = request.PlaylistId });
            }
            else
            {
                state.TrackOrder = result.NextTrackOrder;
                state.CurrentTrackIndex = result.NextTrackIndex;
                state.CurrentTrackStartedAtUtc = DateTime.UtcNow;

                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistTrackChange, new
                {
                    playlistId = request.PlaylistId,
                    trackId = result.NextTrackId,
                    trackIndex = state.CurrentTrackIndex,
                    trackOrder = state.TrackOrder,
                    currentTrackStartedAtUtc = state.CurrentTrackStartedAtUtc,
                });
            }

            return ApiResult.Ok(new { ended = result.Ended });
        }

        [ApiRoute("GET", "api/Playlist/GetCurrentPlayback")]
        public async Task<ApiResult> GetCurrentPlayback(User? user, Guid gameId)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            // Deliberately bypasses MediatR — direct read of in-memory GameLobby state,
            // no DB/business logic involved, mirroring the CmdDebugModeGet precedent.
            var lobby = _lobbyService.GetLobby(gameId);
            var data = lobby?.ActivePlaylistPlaybacks.Values.Select(s => s.ToMessage()).ToList() ?? new();

            return ApiResult.Ok(data);
        }

        private async Task BroadcastPlaylist(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO player, string command, object data)
        {
            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return;

            await lobby.HandlePostCommand(player, new WebSockets.WebSocketCommand
            {
                Command = command,
                Result = WebSockets.Core.WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(data),
            });
        }

        private async Task NotifyPlaylistChange(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO player, string action, Guid id)
        {
            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return;

            await lobby.HandlePostCommand(player, new WebSockets.WebSocketCommand
            {
                Command = WebSockets.Core.WebSocketCommandNames.PlaylistNotify,
                Result  = WebSockets.Core.WebSocketCommandNames.ResultOk,
                Data    = JToken.FromObject(new { action, id, gameId }),
            });
        }

        private async Task<GetPlayerCommandResponse> GetPlayerIfExists(Guid? gameID, User user)
        {
            GetPlayerCommand getPlayer = new GetPlayerCommand();
            getPlayer.GameID = gameID;
            getPlayer.User = user;

            var playerResult = await mediator.Send(getPlayer);
            return playerResult;
        }
    }

}
