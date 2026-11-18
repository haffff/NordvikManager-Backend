using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Controllers
{
    /// <summary>What kind of host this is (see <see cref="HostEndpoints"/>, which has the logic).</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class HostController : Controller
    {
        private readonly HostEndpoints endpoints = new();

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [HttpGet]
        [Authorize]
        [Route("Info")]
        public IActionResult Info([FromServices] IHostDescription host)
            => endpoints.Info(CurrentUser, host).ToActionResult();
    }
}
