using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Soundboard.GetSoundPlayback;
using NordvikManager.Application.Commands.Soundboard.PlaySound;
using NordvikManager.Application.Commands.Soundboard.StopSound;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Domain.Enums;
using NordvikManager.Services;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="SoundboardEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SoundboardController : Controller
    {
        private readonly SoundboardEndpoints endpoints;

        public SoundboardController(IMediator mediator, ILobbyService lobbyService)
        {
            endpoints = new SoundboardEndpoints(mediator, lobbyService);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [HttpPost]
        [Authorize]
        [Route("PlaySound")]
        public async Task<IActionResult> PlaySound([FromQuery] Guid gameId, [FromBody] ResourceIdRequest request)
            => (await endpoints.PlaySound(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("StopSound")]
        public async Task<IActionResult> StopSound([FromQuery] Guid gameId, [FromBody] ResourceIdRequest request)
            => (await endpoints.StopSound(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("StopAllSounds")]
        public async Task<IActionResult> StopAllSounds([FromQuery] Guid gameId)
            => (await endpoints.StopAllSounds(CurrentUser, gameId)).ToActionResult();
    }
}
