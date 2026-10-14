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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Threading.Tasks;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DndOnePlaceManager.Application.Commands.Addons.GetAddon;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DNDOnePlaceManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]    public class AddonController : Controller
    {
        private readonly IMediator mediator;
        private readonly IServiceProvider serviceProvider;
        private readonly ILobbyService lobbyService;
        private readonly DndOnePlaceManager.Application.Interfaces.IGameEventLogger gameEventLogger;

        public AddonController(
            IMediator mediator,
            ILobbyService lobbyService,
            IServiceProvider serviceProvider,
            DndOnePlaceManager.Application.Interfaces.IGameEventLogger gameEventLogger)
        {
            this.mediator = mediator;
            this.serviceProvider = serviceProvider;
            this.lobbyService = lobbyService;
            this.gameEventLogger = gameEventLogger;
        }        /// <summary>
        /// Resolves the lobby for <paramref name="gameId"/> and attaches it to the scoped
        /// game event logger so that MediatR handlers dispatched from this HTTP request
        /// write to the correct in-memory event log.
        /// </summary>
        /// <summary>
        /// Start-to-end trace of addon install/uninstall operations (category "AddonOp"),
        /// so a stuck or failed operation can be followed in the server console.
        /// </summary>
        private static Microsoft.Extensions.Logging.ILogger OpLog(IServiceProvider provider) =>
            provider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger("AddonOp")
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        private void AttachLobbyLog(Guid gameId)
        {
            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null) return;
            (gameEventLogger as DNDOnePlaceManager.Services.Implementations.LobbyGameEventLogger)
                ?.Attach(lobby.EventLog);
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
                StepDefinitions = StepDefinitionMetadata.Build(services)
            };

            return Ok(stepDefinitions);
        }

        [Route("Hooks")]
        [HttpGet]
        public IActionResult GetHooks()
        {
            return Ok(DNDOnePlaceManager.Enums.HookInfo.GetAll());
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
            var log = OpLog(serviceProvider);
            log.LogInformation("[install] request game={GameId} key={Key}", gameId, body?.Key);
            if (string.IsNullOrWhiteSpace(body?.Key))
                return BadRequest(new { error = "key is required." });

            GetPlayerCommandResponse player = await GetPlayer(gameId);
            var operationId = Guid.NewGuid();
            log.LogInformation("[install] op={OperationId} player={Player} ({PlayerId}) key={Key} → starting in background",
                operationId, player.Player?.Name, player.Player?.Id, body.Key);

            RunInstallInBackground(gameId, player.Player, operationId, new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonSourceKey = body.Key,
            });

            log.LogInformation("[install] op={OperationId} answered 200", operationId);
            return Ok(new { started = true, operationId });
        }

        /// <summary>POST addon/update — re-install latest version from repository.</summary>
        [Route("update")]
        [HttpPost]        
        public async Task<IActionResult> UpdateAddon([FromQuery] Guid gameId, [FromBody] InstallAddonRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Key))
                return BadRequest(new { error = "key is required." });

            AttachLobbyLog(gameId);
            GetPlayerCommandResponse player = await GetPlayer(gameId);

            var command = new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonSourceKey = body.Key,
                Reinstall = true,
            };

            await mediator.Send(command);

            return Ok();
        }

        public class UninstallAddonRequest { public string AddonId { get; set; } }

        /// <summary>POST addon/uninstall — uninstall by id or key.</summary>
        [Route("uninstall")]
        [HttpPost]
        public async Task<IActionResult> UninstallAddon([FromQuery] Guid gameId, [FromBody] UninstallAddonRequest body)
        {
            var log = OpLog(serviceProvider);
            log.LogInformation("[uninstall] request game={GameId} addonId={AddonId}", gameId, body?.AddonId);
            if (string.IsNullOrWhiteSpace(body?.AddonId))
                return BadRequest(new { error = "addonId is required." });

            AttachLobbyLog(gameId);
            GetPlayerCommandResponse player = await GetPlayer(gameId);
            log.LogInformation("[uninstall] player={Player} ({PlayerId})", player.Player?.Name, player.Player?.Id);

            Guid? addonGuid = Guid.TryParse(body.AddonId, out var g) ? g : null;

            // ensure addon exists and try to get key for hook call — kept on the request
            // thread (unlike the uninstall itself below) so a bad/missing addonId still
            // gets an immediate 400 instead of surfacing only via a background failure toast.
            var getExistingAddonCommand = new GetAddonCommand()
            {
                GameID = gameId,
                Player = player.Player,
                Id = addonGuid ?? Guid.Empty,
                AddonKey = addonGuid == null ? body.AddonId : null,
            };

            var existingAddon = await mediator.Send(getExistingAddonCommand);

            if (existingAddon == null)
            {
                log.LogWarning("[uninstall] addon {AddonId} not found in game {GameId} → 400", body.AddonId, gameId);
                return BadRequest(new { error = "Addon not found." });
            }

            var operationId = Guid.NewGuid();
            log.LogInformation("[uninstall] op={OperationId} addon={AddonName} id={Id} key={Key} → starting in background",
                operationId, existingAddon.Name, existingAddon.Id, existingAddon.Key);

            RunUninstallInBackground(gameId, player.Player, operationId, existingAddon.Key, new UninstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonId = addonGuid,
                AddonKey = addonGuid == null ? body.AddonId : null,
            });

            log.LogInformation("[uninstall] op={OperationId} answered 200", operationId);
            return Ok(new { started = true, operationId });
        }

        /// <summary>
        /// Fire-and-forget: runs an addon uninstall in its own DI scope (mirrors
        /// RunInstallInBackground) and pushes completion/failure over the lobby's WS/WebRTC
        /// channel via OperationProgressPublisher, keyed by operationId. Uninstalling can
        /// itself run addon-authored actions/queries against the game, which was blocking
        /// the request thread (and, for a game host, its own WebRTC signaling) for as long
        /// as those actions took — same class of bug already fixed for installs.
        /// </summary>
        private void RunUninstallInBackground(Guid gameId, DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO player, Guid operationId, string? addonKey, UninstallAddonCommand command)
        {
            var log = OpLog(serviceProvider);
            log.LogInformation("[uninstall] op={OperationId} queued", operationId);
            _ = Task.Run(async () =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                log.LogInformation("[uninstall] op={OperationId} background started", operationId);
                DNDOnePlaceManager.Services.Implementations.GameLobby? lobby = null;
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var scopedLobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                    lobby = scopedLobbyService.GetLobby(gameId);
                    log.LogInformation("[uninstall] op={OperationId} lobby={Lobby} connectedPlayers={Count}",
                        operationId, lobby != null ? "found" : "MISSING", lobby?.ConnectedPlayers.Count);

                    (scope.ServiceProvider.GetRequiredService<DndOnePlaceManager.Application.Interfaces.IGameEventLogger>()
                        as DNDOnePlaceManager.Services.Implementations.LobbyGameEventLogger)?.Attach(lobby?.EventLog);

                    try
                    {
                        // Bug fix: this used to be an un-awaited fire-and-forget call made right
                        // before the uninstall proceeded synchronously on the request thread — the
                        // hook's own async work raced the addon's data actually being deleted right
                        // after. Now properly awaited, and still ahead of the removal below, so the
                        // addon's own Uninstall action gets to run against still-live data.
                        if (lobby != null)
                        {
                            log.LogInformation("[uninstall] op={OperationId} Uninstall hook → start", operationId);
                            await lobby.ActionProcessingService.CallHookAsync(DNDOnePlaceManager.Enums.Hook.Uninstall, new AddonHookArgs
                            {
                                GameId = gameId,
                                PlayerId = player.Id ?? default,
                                AddonKey = addonKey,
                            });
                            log.LogInformation("[uninstall] op={OperationId} Uninstall hook → done ({Ms} ms)", operationId, watch.ElapsedMilliseconds);
                        }

                        log.LogInformation("[uninstall] op={OperationId} UninstallAddonCommand → start", operationId);
                        var (_, addonInfo) = await scopedMediator.Send(command);
                        log.LogInformation("[uninstall] op={OperationId} UninstallAddonCommand → done ({Ms} ms)", operationId, watch.ElapsedMilliseconds);

                        await DNDOnePlaceManager.Services.OperationProgressPublisher.Complete(
                            lobby, player, operationId, "Addon uninstalled", addonInfo.AddonName);
                        log.LogInformation("[uninstall] op={OperationId} completed, total {Ms} ms", operationId, watch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "[uninstall] op={OperationId} FAILED after {Ms} ms", operationId, watch.ElapsedMilliseconds);
                        await DNDOnePlaceManager.Services.OperationProgressPublisher.Fail(
                            lobby, player, operationId, "Uninstallation failed", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    // Setup failed, or sending the failure itself did: the player hears nothing.
                    log.LogError(ex, "[uninstall] op={OperationId} background task crashed; the player gets no result", operationId);
                }
            });
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

            await mediator.Send(command);

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
                var raw = System.Text.RegularExpressions.Regex
                    .Replace(body.Data, @"^data:[^;]+;base64,", "");
                fileBytes = Convert.FromBase64String(raw);
            }            catch
            {
                return BadRequest(new { error = "data must be a valid base64 string." });
            }

            GetPlayerCommandResponse player = await GetPlayer(gameId);
            var operationId = Guid.NewGuid();
            var log = OpLog(serviceProvider);
            log.LogInformation("[installFromFile] op={OperationId} player={Player} ({PlayerId}) file={FileName} bytes={Bytes} → starting in background",
                operationId, player.Player?.Name, player.Player?.Id, body.FileName, fileBytes.Length);

            RunInstallInBackground(gameId, player.Player, operationId, new InstallAddonCommand
            {
                GameID = gameId,
                Player = player.Player,
                AddonFile = fileBytes,
                AddonFileName = body.FileName,
            });

            log.LogInformation("[installFromFile] op={OperationId} answered 200", operationId);
            return Ok(new { started = true, operationId });
        }

        /// <summary>
        /// Fire-and-forget: runs an addon install in its own DI scope (mirrors
        /// MaterialsController.LinkDirectory) and pushes progress/completion/failure over
        /// the lobby's WS/WebRTC channel via OperationProgressPublisher, keyed by operationId.
        /// </summary>
        private void RunInstallInBackground(Guid gameId, DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO player, Guid operationId, InstallAddonCommand command)
        {
            var log = OpLog(serviceProvider);
            log.LogInformation("[install] op={OperationId} queued", operationId);
            _ = Task.Run(async () =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                log.LogInformation("[install] op={OperationId} background started", operationId);
                DNDOnePlaceManager.Services.Implementations.GameLobby? lobby = null;
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var scopedLobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                    lobby = scopedLobbyService.GetLobby(gameId);
                    log.LogInformation("[install] op={OperationId} lobby={Lobby} connectedPlayers={Count}",
                        operationId, lobby != null ? "found" : "MISSING", lobby?.ConnectedPlayers.Count);

                    (scope.ServiceProvider.GetRequiredService<DndOnePlaceManager.Application.Interfaces.IGameEventLogger>()
                        as DNDOnePlaceManager.Services.Implementations.LobbyGameEventLogger)?.Attach(lobby?.EventLog);

                    try
                    {
                        command.OnProgress = progress =>
                        {
                            log.LogInformation("[install] op={OperationId} progress {Current}/{Total} {Phase}: {Message}",
                                operationId, progress.Current, progress.Total, progress.Phase, progress.Message);
                            DNDOnePlaceManager.Services.OperationProgressPublisher
                                .Update(lobby, player, operationId, progress.Current, progress.Total, progress.Message)
                                .GetAwaiter().GetResult();
                        };

                        log.LogInformation("[install] op={OperationId} InstallAddonCommand → start", operationId);
                        var (installResponse, addonInfo) = await scopedMediator.Send(command);
                        var alreadyInstalled = installResponse == DndOnePlaceManager.Domain.Enums.CommandResponse.AlreadyExists;
                        log.LogInformation("[install] op={OperationId} InstallAddonCommand → {Response} addon={AddonName} key={Key} ({Ms} ms)",
                            operationId, installResponse, addonInfo?.AddonName, addonInfo?.AddonKey, watch.ElapsedMilliseconds);

                        await DNDOnePlaceManager.Services.OperationProgressPublisher.Complete(
                            lobby, player, operationId,
                            alreadyInstalled ? "Addon already installed" : "Addon installed",
                            addonInfo.AddonName);
                        log.LogInformation("[install] op={OperationId} completion sent", operationId);

                        // AlreadyExists means InstallAddonCommandHandler found this addon already
                        // installed and skipped re-processing it entirely (see its own guard) — its
                        // Install hook already ran the first time. Firing it again here would re-run
                        // the addon's install action a second time for no reason.
                        if (lobby != null && !alreadyInstalled)
                        {
                            log.LogInformation("[install] op={OperationId} Install hook → start", operationId);
                            await lobby.ActionProcessingService.CallHookAsync(DNDOnePlaceManager.Enums.Hook.Install, new AddonHookArgs
                            {
                                GameId = gameId,
                                PlayerId = player.Id ?? default,
                                AddonKey = addonInfo.AddonKey,
                            });
                            log.LogInformation("[install] op={OperationId} Install hook → done ({Ms} ms)", operationId, watch.ElapsedMilliseconds);

                            // Mark it fired so RunPendingAddonInstallHooksAsync (run on the next
                            // player join) doesn't fire Install a second time for this addon.
                            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
                            var installedAddon = await dbContext.Addons.FindAsync(addonInfo.AddonId);
                            if (installedAddon != null)
                            {
                                installedAddon.InstallHookFired = true;
                                await dbContext.SaveChangesAsync();
                            }
                        }
                        log.LogInformation("[install] op={OperationId} finished, total {Ms} ms", operationId, watch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "[install] op={OperationId} FAILED after {Ms} ms", operationId, watch.ElapsedMilliseconds);
                        await DNDOnePlaceManager.Services.OperationProgressPublisher.Fail(
                            lobby, player, operationId, "Installation failed", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    // Setup failed, or sending the failure itself did: the player hears nothing.
                    log.LogError(ex, "[install] op={OperationId} background task crashed; the player gets no result", operationId);
                }
            });
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

            var (_, dto) = await mediator.Send(getActionsCommand);

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
        // Event log
        // =========================================================================

        [Route("EventLog")]
        [HttpGet]
        public async Task<IActionResult> GetEventLog([FromQuery] Guid gameId, [FromQuery] string? sinceId)
        {
            var player = await GetPlayer(gameId);
            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return NotFound(new { error = "Game lobby not found." });

            Guid? sinceGuid = Guid.TryParse(sinceId, out var g) ? g : (Guid?)null;
            return Ok(lobby.EventLog.GetEntries(sinceGuid));
        }

        [Route("EventLog")]
        [HttpDelete]
        public async Task<IActionResult> ClearEventLog([FromQuery] Guid gameId)
        {
            var player = await GetPlayer(gameId);
            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            if (player.Player.IsOwner != true &&
                (player.Player.Permission == null || (player.Player.Permission & DndOnePlaceManager.Domain.Enums.Permission.Edit) == 0))
                return Forbid();

            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return NotFound(new { error = "Game lobby not found." });

            lobby.EventLog.Clear();
            return Ok(new { cleared = true });
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

