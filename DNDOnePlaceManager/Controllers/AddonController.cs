using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Actions.ResolveQuery;
using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class AddonController : Controller
    {
        private readonly IMediator mediator;
        private readonly IServiceProvider serviceProvider;
        private readonly ILobbyService lobbyService;

        public AddonController(IMediator mediator, ILobbyService lobbyService, IServiceProvider serviceProvider)
        {
            this.mediator = mediator;
            this.serviceProvider = serviceProvider;
            this.lobbyService = lobbyService;
        }

        // =========================================================================
        // Actions / Steps / Hooks
        // =========================================================================

        [Route("actions")]
        [HttpGet]
        public async Task<IActionResult> GetActions([FromQuery] Guid gameId, int page)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            GetActionsCommand getActionsCommand = new GetActionsCommand()
            {
                GameId = gameId,
                Player = player.Player,
                Page = page,
                flatList = true
            };

            var (resp, dto) = await mediator.Send(getActionsCommand);

            if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
            {
                return BadRequest(resp);
            }

            return Ok(dto);
        }

        [Route("StepDefinitions")]
        [HttpGet]
        public async Task<IActionResult> GetStepDefinitions()
        {
            var services = serviceProvider.GetServices<IActionStepDefinition>();

            GetActionsDefinitionaResponse stepDefinitions = new GetActionsDefinitionaResponse()
            {
                StepDefinitions = services.Select(x => new ActionDefinitionResponse()
                {
                    Name = x.Name,
                    Value = x.Value,
                    Category = x.Category,
                    Description = x.Description,
                    Arguments = x.DataType?
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(y => y.SetMethod?.IsPublic == true)
                        .Select(y => new ActionDefinitionArgument()
                        {
                            Name = y.Name,
                            Type = y.PropertyType.Name,
                            Description = y.GetCustomAttribute<DescriptionAttribute>()?.Description,
                            ConditionField = y.GetCustomAttribute<DNDOnePlaceManager.Models.ShowIfAttribute>()?.Field,
                            ConditionValue = y.GetCustomAttribute<DNDOnePlaceManager.Models.ShowIfAttribute>()?.Value,
                        }).ToArray()
                }).ToArray()
            };

            return Ok(stepDefinitions);
        }

        [Route("Hooks")]
        [HttpGet]
        public async Task<IActionResult> GetHooks()
        {
            var hooks = Enum.GetValues(typeof(Hook)).Cast<Hook>().Select(x => new { Name = x.ToString(), Value = (int)x }).ToArray();
            return Ok(hooks);
        }

        [Route("RunningActions")]
        [HttpGet]
        public async Task<IActionResult> GetRunningActions([FromQuery] Guid gameId)
        {
            var player = await GetPlayer(gameId);

            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return NotFound("Game lobby not found.");

            var entries = lobby.ActionProcessingService.RunningActions.Values
                .Select(x => new
                {
                    x.RunId,
                    x.ActionName,
                    x.StartedAt,
                    x.FinishedAt,
                    State = x.State.ToString(),
                    x.CurrentStep,
                    x.FaultMessage,
                    x.WaitingOnToken
                });

            return Ok(entries);
        }

        [Route("SupplyInput")]
        [HttpPost]
        public async Task<IActionResult> SupplyInput([FromQuery] Guid gameId, [FromQuery] Guid token, [FromBody] object data)
        {
            var player = await GetPlayer(gameId);

            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return NotFound("Game lobby not found.");

            if (!lobby.ActionProcessingService.InputHandler.TryRemove(token, out var tcs))
                return NotFound(new { error = "No action is waiting on that token." });

            var cmd = new WebSocketCommand
            {
                InputToken = token,
                Data = data == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(data),
                Command = WebSocketCommandNames.CmdDebugAction
            }; tcs.TrySetResult(cmd);
            return Ok(new { resolved = token });
        }

        [Route("KillAction")]
        [HttpPost]
        public async Task<IActionResult> KillAction([FromQuery] Guid gameId, [FromQuery] Guid runId)
        {
            var player = await GetPlayer(gameId);

            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return NotFound("Game lobby not found.");

            if (!lobby.ActionProcessingService.RunningActions.TryGetValue(runId, out var entry))
                return NotFound(new { error = "No running action found with that runId." });

            if (entry.State is ActionRunState.Completed or ActionRunState.Faulted or ActionRunState.Killed)
                return Conflict(new { error = $"Action already in terminal state: {entry.State}." });

            entry.Kill();
            return Ok(new { killed = runId, action = entry.ActionName });
        }

        // =========================================================================
        // Addon management
        // =========================================================================

        /// <summary>GET addon/installed — list addons installed on this game.</summary>
        [Route("installed")]
        [HttpGet]
        public async Task<IActionResult> GetInstalledAddons([FromQuery] Guid gameId)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new GetAddonsCommand
            {
                GameId = gameId,
                Player = player.Player,
                Flat = true,
            };

            var dto = await mediator.Send(command);
            return Ok(dto);
        }

        /// <summary>GET addon/repository — list addons available in the remote registry.</summary>
        [Route("repository")]
        [HttpGet]
        public async Task<IActionResult> GetRepository([FromQuery] Guid gameId)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new GetAddonsFromRepositoryCommand();
            var dto = await mediator.Send(command);
            return Ok(dto);
        }

        public class InstallAddonRequest { public string Key { get; set; } }

        /// <summary>POST addon/install — install from repository by key.</summary>
        [Route("install")]
        [HttpPost]
        public async Task<IActionResult> InstallAddon([FromQuery] Guid gameId, [FromBody] InstallAddonRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Key))
                return BadRequest(new { error = "key is required." });

            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonSourceKey = body.Key,
            };

            var (resp, _) = await mediator.Send(command);
            if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                return BadRequest(new { error = resp.ToString() });

            return Ok();
        }

        /// <summary>POST addon/update — re-install latest version from repository.</summary>
        [Route("update")]
        [HttpPost]
        public async Task<IActionResult> UpdateAddon([FromQuery] Guid gameId, [FromBody] InstallAddonRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Key))
                return BadRequest(new { error = "key is required." });

            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonSourceKey = body.Key,
                Reinstall = true,
            };

            var (resp, _) = await mediator.Send(command);
            if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                return BadRequest(new { error = resp.ToString() });

            return Ok();
        }

        public class UninstallAddonRequest { public string AddonId { get; set; } }

        /// <summary>POST addon/uninstall — uninstall by id or key.</summary>
        [Route("uninstall")]
        [HttpPost]
        public async Task<IActionResult> UninstallAddon([FromQuery] Guid gameId, [FromBody] UninstallAddonRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.AddonId))
                return BadRequest(new { error = "addonId is required." });

            GetPlayerCommandResponse player = await GetPlayer(gameId);

            Guid? addonGuid = Guid.TryParse(body.AddonId, out var g) ? g : null;

            var command = new UninstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonId = addonGuid,
                AddonKey = addonGuid == null ? body.AddonId : null,
            };

            var response = await mediator.Send(command);
            if (response != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                return BadRequest(new { error = response.ToString() });

            return Ok();
        }

        public class SetEnabledRequest { public string AddonId { get; set; } public bool Enabled { get; set; } }

        /// <summary>POST addon/setEnabled — enable or disable an installed addon.</summary>
        [Route("setEnabled")]
        [HttpPost]
        public async Task<IActionResult> SetEnabled([FromQuery] Guid gameId, [FromBody] SetEnabledRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.AddonId))
                return BadRequest(new { error = "addonId is required." });

            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new SetAddonEnabledCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonId = body.AddonId,
                Enabled = body.Enabled,
            };

            var response = await mediator.Send(command);
            if (response != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                return BadRequest(new { error = response.ToString() });

            return Ok();
        }

        public class InstallFromFileRequest
        {
            public string FileName { get; set; }
            public string Data { get; set; }
            public string MimeType { get; set; }
        }

        /// <summary>POST addon/installFromFile — install from base64-encoded file.</summary>
        [Route("installFromFile")]
        [HttpPost]
        public async Task<IActionResult> InstallFromFile([FromQuery] Guid gameId, [FromBody] InstallFromFileRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Data))
                return BadRequest(new { error = "data is required." });

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(body.Data);
            }
            catch
            {
                return BadRequest(new { error = "data must be a valid base64 string." });
            }

            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonFile = fileBytes,
                AddonFileName = body.FileName,
            };

            var (resp, _) = await mediator.Send(command);
            if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                return BadRequest(new { error = resp.ToString() });

            return Ok();
        }

        // =========================================================================
        // Legacy / kept for backwards compat
        // =========================================================================

        [Route("addons")]
        [HttpGet]
        public async Task<IActionResult> GetAddons([FromQuery] Guid gameId)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            GetAddonsCommand getAddonsCommand = new GetAddonsCommand()
            {
                GameId = gameId,
                Player = player.Player,
                Flat = true,
            };

            var dto = await mediator.Send(getAddonsCommand);
            return Ok(dto);
        }

        // =========================================================================
        // Cards / Custom panels
        // =========================================================================

        [Route("action")]
        [HttpGet]
        public async Task<IActionResult> GetAction([FromQuery] Guid gameid, [FromQuery] Guid id)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameid);

            GetActionByIdCommand getActionsCommand = new GetActionByIdCommand()
            {
                GameId = gameid,
                Player = player.Player,
                Id = id
            };

            var (resp, dto) = await mediator.Send(getActionsCommand);

            if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
            {
                return BadRequest(resp);
            }

            return Ok(dto);
        }

        [Route("customPanel")]
        [HttpGet]
        public async Task<IActionResult> GetCustomPanel([FromQuery] Guid gameId, [FromQuery] string uiName)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            GetCardCommand getCardCommand = new GetCardCommand()
            {
                Player = player.Player,
                Name = uiName,
                GameID = gameId
            };

            var dto = await mediator.Send(getCardCommand);

            return Ok(dto);
        }

        [Route("customPanels")]
        [HttpGet]
        public async Task<IActionResult> GetCustomPanels([FromQuery] Guid gameId)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            GetAllCardsCommand getCards = new GetAllCardsCommand()
            {
                Player = player.Player,
                GameId = gameId,
                CustomUis = true,
            };

            var (response, dtos) = await mediator.Send(getCards);

            return Ok(dtos);
        }

        // =========================================================================
        // Query resolver
        // =========================================================================

        public class ResolveQueryRequest
        {
            public string Expression { get; set; }
            public Dictionary<string, object> Variables { get; set; }
        }

        /// <summary>POST addon/resolveQuery — resolve %q:%, %qn:%, %v:%, %varName% patterns against the live database.</summary>
        [Route("resolveQuery")]
        [HttpPost]
        public async Task<IActionResult> ResolveQuery([FromQuery] Guid gameId, [FromBody] ResolveQueryRequest body)
        {
            if (string.IsNullOrEmpty(body?.Expression))
                return BadRequest(new { error = "expression is required." });

            var player = await GetPlayer(gameId);
            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var command = new ResolveQueryCommand
            {
                GameId = gameId,
                Player = player.Player,
                Expression = body.Expression,
                Variables = body.Variables ?? new Dictionary<string, object>(),
            };

            var result = await mediator.Send(command);
            return Ok(new { result });
        }

        // =========================================================================
        // Helpers
        // =========================================================================

        private async Task<GetPlayerCommandResponse> GetPlayer(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            return player;
        }
    }
}

