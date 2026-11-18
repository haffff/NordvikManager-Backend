using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// The logic is in <see cref="MapEndpoints"/>, shared with hosts without ASP.NET Core;
    /// this only binds the request and the caller.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : Controller
    {
        private readonly MapEndpoints endpoints;

        public MapController(IMediator mediator)
        {
            endpoints = new MapEndpoints(mediator);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>
        /// Gets a map
        /// </summary>
        /// <returns>map</returns>
        [HttpGet]
        [Authorize]
        [Route("Get")]
        public async Task<IActionResult> GetMap(Guid mapId, Guid gameId)
            => (await endpoints.GetMap(CurrentUser, mapId, gameId)).ToActionResult();

        /// <summary>
        /// Get Flat maps
        /// </summary>
        /// <returns>map</returns>
        [HttpGet]
        [Authorize]
        [Route("GetAllFlat")]
        public async Task<IActionResult> GetAllFlat(Guid gameId)
            => (await endpoints.GetAllFlat(CurrentUser, gameId)).ToActionResult();
    }
}
