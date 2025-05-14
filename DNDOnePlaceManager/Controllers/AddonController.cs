using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class AddonController : Controller
    {
        private readonly IMediator mediator;
        private readonly IServiceProvider serviceProvider;
        private readonly IWebSocketManager wbManager;

        public AddonController(IMediator mediator, IWebSocketManager wbManager, IServiceProvider serviceProvider)
        {
            this.mediator = mediator;
            this.serviceProvider = serviceProvider;
            this.wbManager = wbManager;
        }

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
                    Arguments = x.DataType?.GetProperties().Select(y => new ActionDefinitionArgument()
                    {
                        Name = y.Name,
                        Type = y.PropertyType.Name
                    }).ToArray()
                }).ToArray()
            };
            //figureout why
            return Ok(stepDefinitions);
        }

        [Route("Hooks")]
        [HttpGet]
        public async Task<IActionResult> GetHooks()
        {
            var hooks = Enum.GetValues(typeof(Hook)).Cast<Hook>().Select(x => new { Name = x.ToString(), Value = (int)x }).ToArray();
            return Ok(hooks);
        }

        [Route("AddonsToDowload")]
        [HttpGet]
        public async Task<IActionResult> GetAddonsToDownload()
        {
            GetAddonsFromRepositoryCommand getAddonsFromRepositoryCommand = new GetAddonsFromRepositoryCommand();

            var result = await mediator.Send(getAddonsFromRepositoryCommand);

            return Ok(result);
        }

        //To refactor
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

        public class InstallAddonRequest
        {
            [FromForm(Name= "file")]
            public IFormFile File { get; set; }
            [FromForm(Name = "url")]
            public string Url { get; set; }
            [FromForm(Name = "key")]
            public string Key { get; set; }

        }

        //To refactor
        [Route("install")]
        [Consumes("multipart/form-data")]
        [HttpPost]
        public async Task<IActionResult> InstallAddon([FromQuery] Guid gameId, [FromForm] InstallAddonRequest addon)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            if (addon?.File == null && addon?.Url == null && addon?.Key == null)
            {
                return BadRequest();
            }

            byte[] addonFileBytes = null;
            if (addon.File != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await addon.File.CopyToAsync(memoryStream);
                    addonFileBytes = memoryStream.ToArray();
                }
            }

            InstallAddonCommand installAddonCommand = new InstallAddonCommand()
            {
                AddonFile = addonFileBytes,
                GameID = gameId,
                Player = player.Player,
                AddonSourceKey = addon.Key
            };

            var addonId = await mediator.Send(installAddonCommand);

            return Ok(new { });
        }

        [Route("uninstall")]
        [HttpPost]
        public async Task<IActionResult> UninstallAddon([FromQuery] Guid gameId, [FromQuery]  Guid addonId)
        {
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            UninstallAddonCommand uninstallAddonCommand = new UninstallAddonCommand()
            {
                GameID = gameId,
                Player = player.Player,
                AddonId = addonId
            };

            var response = await mediator.Send(uninstallAddonCommand);

            return Ok(response);
        }

        private async Task<GetPlayerCommandResponse> GetPlayer(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            return player;
        }

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
    }
}
