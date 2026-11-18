using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Player.BanUser;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.Commands.Player.GetLocalPlayers;
using NordvikManager.Application.Commands.Player.KickPlayer;
using NordvikManager.Application.Commands.Player.RemoveUserPlayers;
using NordvikManager.Application.Commands.Player.UnbanUser;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Interfaces;
using NordvikManager.Services;
using NordvikManager.WebRTC;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="UserEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly UserEndpoints endpoints;

        public UserController(ICentralServerService centralServer, IMediator mediator, IConfiguration configuration, ILobbyService lobbyService)
        {
            endpoints = new UserEndpoints(centralServer, mediator, configuration, lobbyService);
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest loginData)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.Login(context, loginData));
        }

        [Authorize]
        [HttpGet]
        [Route("CheckLogin")]
        public IActionResult CheckLogin()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, endpoints.CheckLogin(context));
        }

        [Authorize]
        [HttpGet]
        [Route("Logout")]
        public IActionResult Logout()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, endpoints.Logout(context));
        }

        /// <summary>
        /// Silently refreshes the CentralToken using the stored CentralRefreshToken.
        /// Called by the frontend or other endpoints when the 15-min access token has expired.
        /// Returns 200 with the new token on success, 401 if the refresh token is missing/expired.
        /// The GM's browser uses the returned access token for signaling (it has no Central
        /// cookie of its own), as it does the one StartSession returns.
        /// </summary>
        [Authorize]
        [HttpPost]
        [Route("refresh-central")]
        public async Task<IActionResult> RefreshCentralToken()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.RefreshCentralToken(context));
        }

        [Authorize]
        [HttpGet]
        [Route("UserInfo")]
        public IActionResult GetUserInfo()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, endpoints.GetUserInfo(context));
        }

        [Authorize]
        [HttpGet]
        [Route("GetUserNameById")]
        public async Task<IActionResult> GetUserNameById(string id)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetUserNameById(context, id));
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest registerRequest)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.Register(context, registerRequest));
        }

        [HttpGet]
        [Route("CheckRegistrationKey")]
        public async Task<IActionResult> CheckRegistrationKey(string key)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.CheckRegistrationKey(context, key));
        }

        [HttpGet]
        [Authorize]
        [Route("users")]
        public async Task<IActionResult> Users(int page = 1, int count = 10)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.Users(context, page, count));
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteInvite")]
        public async Task<IActionResult> DeleteInvite([FromQuery] string key)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.DeleteInvite(context, key));
        }

        [HttpGet]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> KeyboardBindings()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.KeyboardBindings(context));
        }

        [HttpPost]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> SaveKeyboardBindings([FromBody] Dictionary<string, string> bindings)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.SaveKeyboardBindings(context, bindings));
        }

        [HttpGet]
        [Authorize]
        [Route("getplayers")]
        public async Task<IActionResult> GetPlayers(int page = 1, int count = 10)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetPlayers(context, page, count));
        }

        [HttpPost]
        [Authorize]
        [Route("kickplayer")]
        public async Task<IActionResult> KickPlayer([FromBody] Models.KickPlayerRequest request)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.KickPlayer(context, request));
        }

        [HttpPost]
        [Authorize]
        [Route("removeuser")]
        public async Task<IActionResult> RemoveUser([FromBody] Models.CentralUserRequest request)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.RemoveUser(context, request));
        }

        [HttpPost]
        [Authorize]
        [Route("banuser")]
        public async Task<IActionResult> BanUser([FromBody] Models.CentralUserRequest request, [FromServices] IWebRTCSessionService webRtcSession)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.BanUser(context, request, webRtcSession));
        }

        [HttpPost]
        [Authorize]
        [Route("unbanuser")]
        public async Task<IActionResult> UnbanUser([FromBody] Models.CentralUserRequest request)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.UnbanUser(context, request));
        }
    }
}
