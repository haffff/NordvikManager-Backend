using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Playlist.AddPlaylist;
using NordvikManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using NordvikManager.Application.Commands.Playlist.DeletePlaylist;
using NordvikManager.Application.Commands.Playlist.GetPlaylists;
using NordvikManager.Application.Commands.Playlist.PausePlaylist;
using NordvikManager.Application.Commands.Playlist.StopPlaylist;
using NordvikManager.Application.Commands.Playlist.UpdatePlaylist;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Domain.Enums;
using NordvikManager.Services.Interfaces;
using NordvikManager.Services;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="PlaylistEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PlaylistController : Controller
    {
        private readonly PlaylistEndpoints endpoints;

        public PlaylistController(IMediator mediator, ILobbyService lobbyService, IPlaybackService playbackService)
        {
            endpoints = new PlaylistEndpoints(mediator, lobbyService, playbackService);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [HttpGet]
        [Authorize]
        [Route("GetPlaylists")]
        public async Task<IActionResult> GetPlaylists([FromQuery] Guid gameId, [FromQuery] PlaylistKind kind = PlaylistKind.Music)
            => (await endpoints.GetPlaylists(CurrentUser, gameId, kind)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("AddPlaylist")]
        public async Task<IActionResult> AddPlaylist([FromQuery] Guid gameId, [FromBody] AddPlaylistRequest request)
            => (await endpoints.AddPlaylist(CurrentUser, gameId, request)).ToActionResult();

        [HttpPut]
        [Authorize]
        [Route("UpdatePlaylist")]
        public async Task<IActionResult> UpdatePlaylist([FromQuery] Guid gameId, [FromBody] UpdatePlaylistRequest request)
            => (await endpoints.UpdatePlaylist(CurrentUser, gameId, request)).ToActionResult();

        [HttpDelete]
        [Authorize]
        [Route("RemovePlaylist")]
        public async Task<IActionResult> RemovePlaylist([FromQuery] Guid gameId, [FromBody] Guid playlistId)
            => (await endpoints.RemovePlaylist(CurrentUser, gameId, playlistId)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("PlayPlaylist")]
        public async Task<IActionResult> PlayPlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
            => (await endpoints.PlayPlaylist(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("PausePlaylist")]
        public async Task<IActionResult> PausePlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
            => (await endpoints.PausePlaylist(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("StopPlaylist")]
        public async Task<IActionResult> StopPlaylist([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
            => (await endpoints.StopPlaylist(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("AdvanceTrack")]
        public async Task<IActionResult> AdvanceTrack([FromQuery] Guid gameId, [FromBody] PlaylistIdRequest request)
            => (await endpoints.AdvanceTrack(CurrentUser, gameId, request)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetCurrentPlayback")]
        public async Task<IActionResult> GetCurrentPlayback([FromQuery] Guid gameId)
            => (await endpoints.GetCurrentPlayback(CurrentUser, gameId)).ToActionResult();
    }
}
