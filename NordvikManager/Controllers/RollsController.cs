using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// "Roll now, post later" (see <see cref="RollsEndpoints"/>, which has the logic, shared
    /// with hosts without ASP.NET Core).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RollsController : Controller
    {
        public const int MaxFormulas = RollsEndpoints.MaxFormulas;
        public const int MaxFormulaLength = RollsEndpoints.MaxFormulaLength;
        public const int MaxKeyLength = RollsEndpoints.MaxKeyLength;
        public const int MaxHtmlLength = RollsEndpoints.MaxHtmlLength;
        public const int MaxCssResourceKeyLength = RollsEndpoints.MaxCssResourceKeyLength;
        public const int MaxTitleLength = RollsEndpoints.MaxTitleLength;

        private readonly RollsEndpoints endpoints;

        public RollsController(IMediator mediator, ILobbyService lobbyService)
        {
            endpoints = new RollsEndpoints(mediator, lobbyService);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [HttpPost]
        [Authorize]
        [Route("Start")]
        public async Task<IActionResult> Start([FromQuery] Guid gameId, [FromBody] StartRollRequest request)
            => (await endpoints.Start(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("Finish")]
        public async Task<IActionResult> Finish([FromQuery] Guid gameId, [FromBody] FinishRollRequest request)
            => (await endpoints.Finish(CurrentUser, gameId, request)).ToActionResult();
    }
}
