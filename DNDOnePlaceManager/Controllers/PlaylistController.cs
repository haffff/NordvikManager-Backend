using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using DndOnePlaceManager.Application.Commands.Playlist.DeletePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists;
using DndOnePlaceManager.Application.Commands.Playlist.PausePlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.StopPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaylistController : Controller
    {
        private readonly IMediator mediator;
        private readonly ILobbyService _lobbyService;

        public PlaylistController(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            _lobbyService = lobbyService;
        }

        [HttpGet]
        [Authorize]
        [Route("GetPlaylists")]
        public async Task<IActionResult> GetPlaylists([FromQuery] Guid gameId, [FromQuery] PlaylistKind kind = PlaylistKind.Music)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new GetPlaylistsCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Kind = kind,
            });

            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("AddPlaylist")]
        public async Task<IActionResult> AddPlaylist([FromQuery] Guid gameId, [FromBody] AddPlaylistRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

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
            });

            await NotifyPlaylistChange(gameId, playerResult.Player, "add", id);

            return Ok(new { id });
        }

        [HttpPut]
        [Authorize]
        [Route("UpdatePlaylist")]
        public async Task<IActionResult> UpdatePlaylist([FromQuery] Guid gameId, [FromBody] UpdatePlaylistRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

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
            });

            await NotifyPlaylistChange(gameId, playerResult.Player, "update", request.Id);

            return Ok(result);
        }

        [HttpDelete]
        [Authorize]
        [Route("RemovePlaylist")]
        public async Task<IActionResult> RemovePlaylist([FromQuery] Guid gameId, [FromBody] Guid playlistId)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new DeletePlaylistCommand
            {
                GameId     = gameId,
                Player     = playerResult.Player,
                PlaylistId = playlistId,
            });

            await NotifyPlaylistChange(gameId, playerResult.Player, "delete", playlistId);

            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("PlayPlaylist")]
        public async Task<IActionResult> PlayPlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return BadRequest();

            // Resume-in-place: if a paused entry already exists for this playlist, unpause it
            // rather than restarting from track 0 / re-shuffling.
            if (lobby.ActivePlaylistPlaybacks.TryGetValue(request.PlaylistId, out var existing) && existing.IsPaused)
            {
                existing.IsPaused = false;
                existing.CurrentTrackStartedAtUtc = DateTime.UtcNow;

                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistPlay, new
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

                return Ok(new { response = CommandResponse.Ok });
            }

            var result = await mediator.Send(new PlayPlaylistCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                PlaylistId = request.PlaylistId,
            });

            if (result.Response == CommandResponse.Ok && result.TrackOrder.Count > 0)
            {
                var state = new Services.Implementations.PlaylistPlaybackState
                {
                    PlaylistId = request.PlaylistId,
                    Mode = result.Mode,
                    Shuffle = result.Shuffle,
                    Repeat = result.Repeat,
                    TrackOrder = result.TrackOrder,
                    CurrentTrackIndex = 0,
                    IsPaused = false,
                    CurrentTrackStartedAtUtc = DateTime.UtcNow,
                };
                lobby.ActivePlaylistPlaybacks[request.PlaylistId] = state;

                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistPlay, new
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

            return Ok(new { response = result.Response });
        }

        [HttpPost]
        [Authorize]
        [Route("PausePlaylist")]
        public async Task<IActionResult> PausePlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new PausePlaylistCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                PlaylistId = request.PlaylistId,
            });

            var lobby = _lobbyService.GetLobby(gameId);
            if (result == CommandResponse.Ok && lobby != null &&
                lobby.ActivePlaylistPlaybacks.TryGetValue(request.PlaylistId, out var state))
            {
                state.IsPaused = true;
                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistPause,
                    new { playlistId = request.PlaylistId });
            }

            return Ok(new { response = result });
        }

        [HttpPost]
        [Authorize]
        [Route("StopPlaylist")]
        public async Task<IActionResult> StopPlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new StopPlaylistCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                PlaylistId = request.PlaylistId,
            });

            var lobby = _lobbyService.GetLobby(gameId);
            if (result == CommandResponse.Ok && lobby != null)
            {
                lobby.ActivePlaylistPlaybacks.TryRemove(request.PlaylistId, out _);
                await BroadcastPlaylist(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.PlaylistStop,
                    new { playlistId = request.PlaylistId });
            }

            return Ok(new { response = result });
        }

        [HttpPost]
        [Authorize]
        [Route("AdvanceTrack")]
        public async Task<IActionResult> AdvanceTrack([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null || !lobby.ActivePlaylistPlaybacks.TryGetValue(request.PlaylistId, out var state))
                return Ok(new { ended = true });

            // Idempotency guard: if the caller's assumed current-track-index no longer
            // matches the authoritative in-memory state, someone else already advanced
            // this playlist (e.g. a second GM tab reacting to the same track ending) —
            // no-op rather than skip a track ahead.
            if (request.FromTrackIndex.HasValue && request.FromTrackIndex.Value != state.CurrentTrackIndex)
                return Ok(new { ended = false, skipped = true });

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
                return Ok(new { response = result.Response });

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

            return Ok(new { ended = result.Ended });
        }

        [HttpGet]
        [Authorize]
        [Route("GetCurrentPlayback")]
        public async Task<IActionResult> GetCurrentPlayback([FromQuery] Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            // Deliberately bypasses MediatR — direct read of in-memory GameLobby state,
            // no DB/business logic involved, mirroring the CmdDebugModeGet precedent.
            var lobby = _lobbyService.GetLobby(gameId);
            var data = lobby?.ActivePlaylistPlaybacks.Values.Select(s => new
            {
                playlistId = s.PlaylistId,
                mode = s.Mode,
                shuffle = s.Shuffle,
                repeat = s.Repeat,
                trackOrder = s.TrackOrder,
                currentTrackIndex = s.CurrentTrackIndex,
                isPaused = s.IsPaused,
                currentTrackStartedAtUtc = s.CurrentTrackStartedAtUtc,
            }).ToList() ?? new();

            return Ok(data);
        }

        private async Task BroadcastPlaylist(Guid gameId, DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO player, string command, object data)
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

        private async Task NotifyPlaylistChange(Guid gameId, DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO player, string action, Guid id)
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
