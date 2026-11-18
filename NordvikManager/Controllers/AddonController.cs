using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Actions.GetActions;
using NordvikManager.Application.Commands.Actions.ResolveQuery;
using NordvikManager.Application.Commands.Addons.GetAddon;
using NordvikManager.Application.Commands.Addons.GetAddonCategories;
using NordvikManager.Application.Commands.Addons.GetAddonReadme;
using NordvikManager.Application.Commands.Addons.GetAddons;
using NordvikManager.Application.Commands.Addons.GetAddonsFromRepository;
using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Application.Commands.Addons.SetAddonEnabled;
using NordvikManager.Application.Commands.Addons.UninstallAddon;
using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Models;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Services;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="AddonEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Authorize]
    [Route("api/[controller]")]
    public class AddonController : Controller
    {
        private readonly AddonEndpoints endpoints;

        public AddonController(
            IMediator mediator,
            ILobbyService lobbyService,
            IServiceProvider serviceProvider,
            NordvikManager.Application.Interfaces.IGameEventLogger gameEventLogger)
        {
            endpoints = new AddonEndpoints(mediator, lobbyService, serviceProvider, gameEventLogger);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        // The request bodies are AddonEndpoints' (shared); these keep their names here.
        public class InstallAddonRequest : AddonEndpoints.InstallAddonRequest { }
        public class UninstallAddonRequest : AddonEndpoints.UninstallAddonRequest { }
        public class SetEnabledRequest : AddonEndpoints.SetEnabledRequest { }
        public class InstallFromFileRequest : AddonEndpoints.InstallFromFileRequest { }
        public class ResolveQueryRequest : AddonEndpoints.ResolveQueryRequest { }

        [Route("actions")]
        [HttpGet]
        // Without a page: every action (the Actions panel lists them all).
        public async Task<IActionResult> GetActions([FromQuery] Guid gameId, int? page)
            => (await endpoints.GetActions(CurrentUser, gameId, page)).ToActionResult();

        [Route("StepDefinitions")]
        [HttpGet]
        public async Task<IActionResult> GetStepDefinitions()
            => (await endpoints.GetStepDefinitions(CurrentUser)).ToActionResult();

        [Route("Hooks")]
        [HttpGet]
        public IActionResult GetHooks()
            => endpoints.GetHooks(CurrentUser).ToActionResult();

        [Route("RunningActions")]
        [HttpGet]
        public async Task<IActionResult> GetRunningActions([FromQuery] Guid gameId)
            => (await endpoints.GetRunningActions(CurrentUser, gameId)).ToActionResult();

        [Route("SupplyInput")]
        [HttpPost]
        public async Task<IActionResult> SupplyInput([FromQuery] Guid gameId, [FromQuery] Guid token, [FromBody] object data)
            => (await endpoints.SupplyInput(CurrentUser, gameId, token, data)).ToActionResult();

        [Route("KillAction")]
        [HttpPost]
        public async Task<IActionResult> KillAction([FromQuery] Guid gameId, [FromQuery] Guid runId)
            => (await endpoints.KillAction(CurrentUser, gameId, runId)).ToActionResult();

        /// <summary>GET addon/installed — list addons installed on this game.</summary>
        [Route("installed")]
        [HttpGet]
        public async Task<IActionResult> GetInstalledAddons([FromQuery] Guid gameId)
            => (await endpoints.GetInstalledAddons(CurrentUser, gameId)).ToActionResult();

        /// <summary>GET addon/repository — list addons available in the remote registry.</summary>
        [Route("repository")]
        [HttpGet]
        public async Task<IActionResult> GetRepository([FromQuery] Guid gameId)
            => (await endpoints.GetRepository(CurrentUser, gameId)).ToActionResult();

        /// <summary>GET addon/categories — the remote registry's categories ({ key, name, description }).</summary>
        [Route("categories")]
        [HttpGet]
        public async Task<IActionResult> GetCategories([FromQuery] Guid gameId)
            => (await endpoints.GetCategories(CurrentUser, gameId)).ToActionResult();

        /// <summary>
        /// GET addon/readme?key= — an addon's markdown readme ({ key, url, markdown }), read from the
        /// URL in its registry entry. 404 when it has none; 502 when it can't be read.
        /// </summary>
        [Route("readme")]
        [HttpGet]
        public async Task<IActionResult> GetReadme([FromQuery] Guid gameId, [FromQuery] string key)
            => (await endpoints.GetReadme(CurrentUser, gameId, key)).ToActionResult();

        /// <summary>POST addon/install — install from repository by key.</summary>
        [Route("install")]
        [HttpPost]
        public async Task<IActionResult> InstallAddon([FromQuery] Guid gameId, [FromBody] InstallAddonRequest body)
            => (await endpoints.InstallAddon(CurrentUser, gameId, body)).ToActionResult();

        /// <summary>POST addon/update — re-install latest version from repository.</summary>
        [Route("update")]
        [HttpPost]
        public async Task<IActionResult> UpdateAddon([FromQuery] Guid gameId, [FromBody] InstallAddonRequest body)
            => (await endpoints.UpdateAddon(CurrentUser, gameId, body)).ToActionResult();

        /// <summary>POST addon/uninstall — uninstall by id or key.</summary>
        [Route("uninstall")]
        [HttpPost]
        public async Task<IActionResult> UninstallAddon([FromQuery] Guid gameId, [FromBody] UninstallAddonRequest body)
            => (await endpoints.UninstallAddon(CurrentUser, gameId, body)).ToActionResult();

        /// <summary>POST addon/setEnabled — enable or disable an installed addon.</summary>
        [Route("setEnabled")]
        [HttpPost]
        public async Task<IActionResult> SetEnabled([FromQuery] Guid gameId, [FromBody] SetEnabledRequest body)
            => (await endpoints.SetEnabled(CurrentUser, gameId, body)).ToActionResult();

        /// <summary>POST addon/installFromFile — install from base64-encoded file.</summary>
        [Route("installFromFile")]
        [HttpPost]
        public async Task<IActionResult> InstallFromFile([FromQuery] Guid gameId, [FromBody] InstallFromFileRequest body)
            => (await endpoints.InstallFromFile(CurrentUser, gameId, body)).ToActionResult();

        [Route("addons")]
        [HttpGet]
        public async Task<IActionResult> GetAddons([FromQuery] Guid gameId)
            => (await endpoints.GetAddons(CurrentUser, gameId)).ToActionResult();

        [Route("action")]
        [HttpGet]
        public async Task<IActionResult> GetAction([FromQuery] Guid gameid, [FromQuery] Guid id)
            => (await endpoints.GetAction(CurrentUser, gameid, id)).ToActionResult();

        [Route("customPanel")]
        [HttpGet]
        public async Task<IActionResult> GetCustomPanel([FromQuery] Guid gameId, [FromQuery] string uiName)
            => (await endpoints.GetCustomPanel(CurrentUser, gameId, uiName)).ToActionResult();

        [Route("customPanels")]
        [HttpGet]
        public async Task<IActionResult> GetCustomPanels([FromQuery] Guid gameId)
            => (await endpoints.GetCustomPanels(CurrentUser, gameId)).ToActionResult();

        /// <summary>POST addon/resolveQuery — fill in an expression's tokens (%name%, %name.path%, %q:%) as a running action would.</summary>
        [Route("resolveQuery")]
        [HttpPost]
        public async Task<IActionResult> ResolveQuery([FromQuery] Guid gameId, [FromBody] ResolveQueryRequest body)
            => (await endpoints.ResolveQuery(CurrentUser, gameId, body)).ToActionResult();

        [Route("EventLog")]
        [HttpGet]
        public async Task<IActionResult> GetEventLog([FromQuery] Guid gameId, [FromQuery] string? sinceId)
            => (await endpoints.GetEventLog(CurrentUser, gameId, sinceId)).ToActionResult();

        [Route("EventLog")]
        [HttpDelete]
        public async Task<IActionResult> ClearEventLog([FromQuery] Guid gameId)
            => (await endpoints.ClearEventLog(CurrentUser, gameId)).ToActionResult();
    }
}
