using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    internal class ActionProcessingService : IActionProcessingService
    {
        private IServiceScopeFactory serviceScopeFactory;

        // Stripped from hook data before actions see it (settings_game carries the new game password).
        private static readonly string[] SensitiveHookDataKeys = { "password" };

        // The run whose step is currently executing on this async flow. A sub-action started
        // by that step (If/ForEach/ExecuteAction) sees it as its parent.
        private static readonly AsyncLocal<ActionRunEntry> CurrentRun = new();

        private readonly Dictionary<string, IActionStepDefinition> actionStepDefinitions = new Dictionary<string, IActionStepDefinition>();

        public ActionProcessingService(IServiceScopeFactory serviceScopeFactory)
        {
            this.serviceScopeFactory = serviceScopeFactory;
            this.serviceScopeFactory.CreateScope()
                .ServiceProvider.GetServices<IActionStepDefinition>()
                .ToList().ForEach(x => actionStepDefinitions.Add(x.Value, x));
        }

        public GameLobby GameLobby { get; set; }        /// <summary>
                                                        /// Put here commands that are feedback from user for action processing
                                                        /// </summary>
        public ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>> InputHandler { get; init; } = new ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>>();

        /// <summary>
        /// Tracks all currently running (and recently finished) action executions.
        /// </summary>
        public ConcurrentDictionary<Guid, ActionRunEntry> RunningActions { get; } = new ConcurrentDictionary<Guid, ActionRunEntry>();

        public async Task CallHookAsync(Hook hook, HookArgs.HookArgs hookArg)
        {
            // Hooks can be fired from inside a running action (e.g. SetProperty → Property
            // Updated via Task.Run, which carries this AsyncLocal along). Hook actions are
            // independent runs, not sub-actions of that step.
            CurrentRun.Value = null;
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var actions = await GetActionsAsync(mediator);
                var actList = actions.Where(x => x.Hook == hook && x.IsEnabled).ToList();

                foreach (var action in actList)
                    await ExecActionAsync(action, hookArg, null, mediator);
            }
            catch (Exception e)
            {
                GameLobby?.EventLog?.Log("Error", "Action", $"Hook '{hook}' failed: {e.Message}",
                    details: new { hook, exceptionType = e.GetType().Name });
            }
        }

        /// <summary>
        /// Fires Hook.Install for any of this game's addons that never got it — e.g. a
        /// featured addon auto-installed at game creation (AddGameCommandHandler), which
        /// happens before any GameLobby/ActionProcessingService exists to fire the hook
        /// through. Called whenever a player joins (see LobbyConnectionHelper) so a newly
        /// created game's addons finish "installing" as soon as the lobby is actually up,
        /// instead of never running their Install hook at all. Safe to call repeatedly —
        /// AddonModel.InstallHookFired makes it a no-op once an addon has already run it,
        /// whether that happened here or via the addon-menu install path.
        /// </summary>
        public async Task RunPendingAddonInstallHooksAsync()
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

                var game = await dbContext.Games.Include(x => x.Addons)
                    .FirstOrDefaultAsync(x => x.Id == GameLobby.GameId);
                var pendingAddons = game?.Addons?.Where(a => !a.InstallHookFired).ToList()
                    ?? new List<AddonModel>();

                foreach (var addon in pendingAddons)
                {
                    await CallHookAsync(Hook.Install, new HookArgs.AddonHookArgs
                    {
                        GameId = GameLobby.GameId,
                        PlayerId = GameLobby.SystemPlayer?.Id ?? default,
                        AddonKey = addon.Key,
                    });
                    addon.InstallHookFired = true;
                }

                if (pendingAddons.Count > 0)
                    await dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                GameLobby?.EventLog?.Log("Error", "Action", $"RunPendingAddonInstallHooksAsync failed: {e.Message}",
                    details: new { exceptionType = e.GetType().Name });
            }
        }

        public async Task CommandToHook(WebSocketCommand webSocketCommand)
        {
            try
            {
                var hooks = CommandHooks.HooksFor(webSocketCommand);
                if (hooks.Count == 0)
                    return;

                var player = webSocketCommand.PlayerId.HasValue
                    ? GameLobby.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == webSocketCommand.PlayerId)
                    : null;
                var hookCommand = WithoutSensitiveData(webSocketCommand);

                foreach (var h in hooks)
                {
                    await CallHookAsync(h, new HookArgs.CommandHookArgs()
                    {
                        Command = hookCommand,
                        Player = player,
                        Data = hookCommand.Data as JObject,
                    });
                }
            }
            catch (Exception e)
            {
                GameLobby?.EventLog?.Log("Error", "Action", $"CommandToHook for '{webSocketCommand.Command}' failed: {e.Message}",
                    details: new { command = webSocketCommand.Command, exceptionType = e.GetType().Name });
            }
        }

        /// <summary>
        /// Returns the command unchanged, or a copy with sensitive keys removed from its data
        /// (a copy because the original is still being broadcast concurrently).
        /// </summary>
        private static WebSocketCommand WithoutSensitiveData(WebSocketCommand command)
        {
            if (command.Data is not JObject data || !SensitiveHookDataKeys.Any(k => data.ContainsKey(k)))
                return command;

            var cleaned = (JObject)data.DeepClone();
            foreach (var key in SensitiveHookDataKeys)
                cleaned.Remove(key);

            return new WebSocketCommand
            {
                PlayerId = command.PlayerId,
                GameId = command.GameId,
                Command = command.Command,
                Data = cleaned,
                Result = command.Result,
                OnlyToSender = command.OnlyToSender,
                ElementIds = command.ElementIds,
                BattleMapId = command.BattleMapId,
                Action = command.Action,
                InputToken = command.InputToken,
            };
        }

        public async Task<bool> HasEnabledHookAsync(Hook hook)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var actions = await GetActionsAsync(mediator);
            return actions.Any(x => x.Hook == hook && x.IsEnabled);
        }

        private async Task<List<ActionDto>> GetActionsAsync(IMediator mediator)
        {
            var getActionsCommand = new GetActionsCommand()
            {
                GameId = GameLobby.GameId,
                Player = GameLobby.SystemPlayer,
                flatList = false,
            };

            var (resp, result) = await mediator.Send(getActionsCommand);

            if (resp != CommandResponse.Ok)
                throw new Exception(WebSocketCommandNames.ErrFailedToGetActions);

            return result;
        }
        public async Task ExecActionAsync(ActionDto action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null, IMediator mediator = null, ActionTrace trace = null)
        {
            // Always create a scope so we can get dbContext for the property query resolver.
            // If mediator was already provided by a caller, we still need our own scope for dbContext.
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            mediator ??= scope.ServiceProvider.GetRequiredService<IMediator>();

            var entry = new ActionRunEntry { ActionName = action.Name, Trace = trace };
            RunningActions[entry.RunId] = entry;

            // A sub-action (If/ForEach/ExecuteAction) runs inside its caller's step; remember the
            // caller so a failure here can be reported on that step.
            var parentRun = CurrentRun.Value;
            CurrentRun.Value = entry;
            var fullName = string.IsNullOrEmpty(action.Prefix) ? action.Name : $"{action.Prefix}/{action.Name}";

            var stepIndex = -1;
            string stepId = null, stepType = null;

            try
            {
                var steps = JArray.Parse(action.Content);

                // Start with an empty dict, merge caller-supplied args first so that
                // trusted system variables written below always win and cannot be spoofed.
                var variables = new Dictionary<string, object>();
                if (sharedVariables != null)
                    foreach (var kv in sharedVariables)
                        variables[kv.Key] = kv.Value;

                FillHookArgs(hookArg, variables);

                // Pre-fill standard variables available to every action — these always
                // overwrite any same-named key from sharedVariables to prevent spoofing.
                variables["gameId"]       = GameLobby.GameId.ToString();
                variables["actionId"]     = action.Id.ToString();
                variables["actionName"]   = action.Name ?? string.Empty;
                variables["actionPrefix"] = action.Prefix ?? string.Empty;

                if (variables.TryGetValue("Player", out var pObj) && pObj is PlayerDTO pd)
                {
                    variables["playerId"]      = pd.Id?.ToString() ?? string.Empty;
                    variables["playerName"]    = pd.Name ?? string.Empty;
                    variables["playerColor"]   = pd.Color ?? string.Empty;
                    variables["playerIsOwner"] = pd.IsOwner?.ToString()?.ToLower() ?? "false";
                }

                var game = await dbContext.Games.FindAsync(GameLobby.GameId);
                if (game != null)
                    variables["gmId"] = game.MasterId.ToString();

                await DebugLog(mediator, DebugLogData.Starting(action, steps, variables), entry: entry);

                foreach (var step in steps)
                {
                    stepIndex++;
                    stepId = step["id"]?.ToString();
                    stepType = step[WebSocketCommandNames.StepTypeKey]?.ToString();
                    entry.CancellationToken.ThrowIfCancellationRequested();

                    var debugCmd = await DebugLog(mediator, DebugLogData.ExecutingStep(action, step, variables), entry: entry);

                    if (debugCmd != null && debugCmd.Data.ToString() == WebSocketCommandNames.DebugStopSignal)
                    {
                        entry.SetCompleted();
                        trace?.Finished(GameLobby, "Exited", "Stopped from the debug console.");
                        return;
                    }

                    if (!actionStepDefinitions.TryGetValue(step[WebSocketCommandNames.StepTypeKey].ToString(), out var stepDefinition))
                    {
                        await DebugLog(mediator, DebugLogData.StepNotFound(step), false, entry: entry);
                        trace?.Step(GameLobby, stepIndex, stepId, stepType, "failed", $"Unknown step type '{stepType}', skipped.");
                        continue;
                    }
                    else
                    {
                        var stepObject = step as JObject;
                        entry.SetStep(stepType);
                        trace?.Step(GameLobby, stepIndex, stepId, stepType, "running");
                        var childFaultsBefore = entry.ChildFaultCount;

                        // Resolve %q:% / %qn:% query patterns first, then standard %varName% substitution.
                        // Skip "DefaultValue" tokens — those are resolved lazily inside the step definition
                        // after Value has been evaluated, so a %var% default isn't erased by an empty variable.
                        // Likewise skip any argument the step declares as deferred (IDeferredArgumentsStep).
                        var deferred = (stepDefinition as IDeferredArgumentsStep)?.DeferredArguments;
                        var resolver = new ActionPropertyQueryResolver(dbContext, GameLobby.GameId);
                        foreach (var token in stepObject.Descendants().OfType<JValue>())
                        {
                            if (token.Parent is JProperty jp &&
                                (jp.Name == "DefaultValue" || (deferred != null && deferred.Contains(jp.Name, StringComparer.OrdinalIgnoreCase))))
                                continue;

                            var raw = token.Value?.ToString() ?? string.Empty;
                            raw = await resolver.PreResolveQueriesAsync(raw, variables);
                            token.Value = raw.Prepare(variables);
                        }

                        await stepDefinition.Execute(mediator, variables, GameLobby, step.ToObject<ActionStep>());

                        if (trace != null)
                        {
                            // A sub-action's error doesn't throw here, so check what it recorded.
                            var childError = entry.ChildFaultsSince(childFaultsBefore);
                            trace.Step(GameLobby, stepIndex, stepId, stepType, childError == null ? "done" : "failed",
                                childError, VariableSnapshot.Build(variables, ActionTrace.MaxVariableChars));
                        }
                    }
                }
                await DebugLog(mediator, DebugLogData.Finishing(action, steps, variables), entry: entry);
                entry.SetCompleted();
                trace?.Finished(GameLobby, "Completed");
            }
            catch (OperationCanceledException)
            {
                // entry.Kill() already set State = Killed; nothing more to do
                trace?.Finished(GameLobby, "Killed");
            }
            catch (ActionExitException exit)
            {
                if (!string.IsNullOrWhiteSpace(exit.ExitMessage))
                    GameLobby?.EventLog?.Log("Info", "Action", $"Action '{action.Name}' exited: {exit.ExitMessage}",
                        details: new { actionName = action.Name, actionId = action.Id });
                entry.SetCompleted();
                trace?.Step(GameLobby, stepIndex, stepId, stepType, "exited", exit.ExitMessage);
                trace?.Finished(GameLobby, "Exited", exit.ExitMessage);
            }
            catch (Exception e)
            {
                entry.SetFaulted(e.Message);
                GameLobby?.EventLog?.Log("Error", "Action", $"Action '{action.Name}' failed: {e.Message}",
                    details: new { actionName = action.Name, actionId = action.Id, lastStep = entry.CurrentStep, exceptionType = e.GetType().Name });
                await DebugLog(mediator, DebugLogData.Fault(e.Message), false, entry: entry);
                parentRun?.AddChildFault($"{fullName} failed: {e.Message}");
                trace?.Step(GameLobby, stepIndex, stepId, stepType, "failed", e.Message);
                trace?.Finished(GameLobby, "Faulted", e.Message);
            }
            finally
            {
                // Keep the entry briefly so callers can observe terminal state, then remove it
                _ = Task.Delay(TimeSpan.FromSeconds(30))
                        .ContinueWith(__ => RunningActions.TryRemove(entry.RunId, out _));
            }
        }

        public async Task ExecActionAsync(string action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null, ActionTrace trace = null)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

            // Tracing shows every variable after every step, so only the GM or players who may
            // edit the game get it; anyone else's run just goes ahead untraced.
            if (trace != null && !await CanTraceAsync(mediator, dbContext, trace.Player))
                trace = null;

            List<ActionDto> allActions;
            try
            {
                allActions = await GetActionsAsync(mediator);
            }
            catch (Exception e)
            {
                GameLobby?.EventLog?.Log("Error", "Action", $"Failed to resolve action '{action}': {e.Message}",
                    details: new { action, exceptionType = e.GetType().Name });
                return;
            }

            ActionDto foundActionDto;
            if (action != null && action.Contains('/'))
            {
                var slash = action.IndexOf('/');
                var prefix = action[..slash];
                var name   = action[(slash + 1)..];
                foundActionDto = allActions.FirstOrDefault(x => x.Prefix == prefix && x.Name == name);
            }
            else
            {
                foundActionDto = allActions.FirstOrDefault(x => x.Name == action);
            }

            if (foundActionDto == null)
            {
                // Called from a step (If branch, ExecuteAction…): tell that step its target is missing.
                CurrentRun.Value?.AddChildFault($"action '{action}' not found");
                trace?.Finished(GameLobby, "Faulted", $"Action '{action}' not found.");
                return;
            }

            // This overload is reached whenever an action is looked up BY NAME — the only
            // caller that puts an attacker-controlled name here is GameLobby's CmdExecuteAction
            // handler, which forwards whatever action name a connected client sent over the
            // socket. Hook-triggered and nested (ExecuteAction step) invocations either call the
            // ActionDto overload directly or inherit an already-checked Player from
            // sharedVariables, so gating here — instead of inside the ActionDto overload —
            // enforces the boundary exactly where untrusted input enters without re-checking
            // (and breaking) system/hook-triggered chains that have no live requesting player.
            if (foundActionDto.GmPermission.HasValue &&
                hookArg is HookArgs.CommandHookArgs cmdArgs &&
                cmdArgs.Player != null)
            {
                // InstallAddonCommandHandler grants game.MasterId this exact permission on the
                // action entity itself when GmPermission is set from an addon's JSON definition —
                // that's the per-action ACL row this check is meant to honor. As a fallback (for
                // actions that predate that grant, or were never round-tripped through an
                // install/update path that populates it) we accept the game's master directly,
                // rather than a generic permission check that any other Permission.All grant on
                // the game entity would also satisfy.
                var hasPermission = false;

                if (foundActionDto.Id.HasValue)
                {
                    hasPermission = await mediator.Send(new CheckPermissionsCommand
                    {
                        Player = cmdArgs.Player,
                        EntityId = foundActionDto.Id.Value,
                        RequiredPermission = foundActionDto.GmPermission.Value,
                    });
                }

                if (!hasPermission)
                {
                    var game = await dbContext.Games.FindAsync(GameLobby.GameId);
                    hasPermission = game != null && cmdArgs.Player.Id == game.MasterId;
                }

                if (!hasPermission)
                {
                    GameLobby?.EventLog?.Log("Warning", "Permission",
                        $"Player '{cmdArgs.Player.Name}' attempted to execute action '{action}' without sufficient permission.",
                        cmdArgs.Player.Name);
                    trace?.Finished(GameLobby, "Faulted", "You don't have permission to run this action.");
                    return;
                }
            }

            await ExecActionAsync(foundActionDto, hookArg, sharedVariables, mediator, trace);
        }

        private async Task<bool> CanTraceAsync(IMediator mediator, IDbContext dbContext, PlayerDTO player)
        {
            if (player?.Id == null)
                return false;
            var game = await dbContext.Games.FindAsync(GameLobby.GameId);
            if (game != null && game.MasterId == player.Id)
                return true;
            return await mediator.Send(new CheckPermissionsCommand
            {
                Player = player,
                EntityId = GameLobby.GameId,
                RequiredPermission = DndOnePlaceManager.Domain.Enums.Permission.Edit,
            });
        }
        private async Task<WebSocketCommand> DebugLog(IMediator mediator, object data, bool expectInput = true, ActionRunEntry entry = null)
        {
            if (!GameLobby.Debug)
                return null; var cmd = new WebSocketCommand()
                {
                    Command = WebSocketCommandNames.CmdDebugAction,
                    Data = JObject.FromObject(data),
                };

            if (!expectInput)
            {
                GameLobby.Broadcast(cmd, GameLobby.SystemPlayer);
                return null;
            }

            var token = Guid.NewGuid();
            cmd.InputToken = token;

            var tcs = new TaskCompletionSource<WebSocketCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            InputHandler[token] = tcs; GameLobby.Broadcast(cmd, GameLobby.SystemPlayer);

            entry?.SetWaitingForInput(token);
            try
            {
                // Await the TCS, but respect cancellation (Kill) if wired
                var ct = entry?.CancellationToken ?? CancellationToken.None;
                using var reg = ct.Register(() =>
                {
                    if (InputHandler.TryRemove(token, out var t))
                        t.TrySetCanceled();
                });
                return await tcs.Task;
            }
            finally
            {
                entry?.ClearWaitingForInput();
            }
        }

        private static void FillHookArgs(HookArgs.HookArgs hookArg, Dictionary<string, object> variables)
        {
            if(hookArg == null)
                return;
            foreach (var item in hookArg.GetType().GetProperties())
                variables[item.Name] = item.GetValue(hookArg);
        }
    }

    /// <summary>Which hooks a processed WebSocket command fires.</summary>
    public static class CommandHooks
    {
        private static readonly Dictionary<string, Hook> commandsToHooks = new Dictionary<string, Hook>()
        {
            { WebSocketCommandNames.ElementAdd,      Hook.ElementAdd },
            { WebSocketCommandNames.ElementUpdate,   Hook.ElementUpdate },
            { WebSocketCommandNames.ElementRemove,   Hook.ElementRemove },
            { WebSocketCommandNames.CmdChatPush,     Hook.ChatMessage },
            // WebSocketCommandNames.CmdChatPush, Hook.ChatCommand
            { WebSocketCommandNames.MapAdd,          Hook.MapAdd },
            { WebSocketCommandNames.MapUpdate,       Hook.MapUpdate },
            { WebSocketCommandNames.MapRemove,       Hook.MapRemove },
            { WebSocketCommandNames.MapChange,       Hook.MapChange },
            { WebSocketCommandNames.PlayerJoin,      Hook.PlayerJoin },
            { WebSocketCommandNames.PlayerLeave,     Hook.PlayerLeave },
            { WebSocketCommandNames.PropertyAdd,     Hook.PropertyAdd },
            { WebSocketCommandNames.PropertyUpdate,  Hook.PropertyUpdate },
            { WebSocketCommandNames.PropertyRemove,  Hook.PropertyRemove },
            { WebSocketCommandNames.CardAdd,         Hook.CardAdd },
            { WebSocketCommandNames.CardUpdate,      Hook.CardUpdate },
            { WebSocketCommandNames.CardDelete,      Hook.CardDelete },
            { WebSocketCommandNames.SettingsGame,    Hook.GameUpdate },
            // Only when the turn really changed (see HooksFor).
            { WebSocketCommandNames.TurnOrderAdd,     Hook.TurnChange },
            { WebSocketCommandNames.TurnOrderRemove,  Hook.TurnChange },
            { WebSocketCommandNames.TurnOrderAdvance, Hook.TurnChange },
            { WebSocketCommandNames.TurnOrderEndTurn, Hook.TurnChange },
            { WebSocketCommandNames.TurnOrderReset,   Hook.TurnChange },
        };

        // Frontend tags a drag's element_update with Action = "drag" (OnNativeObjectModifiedClientBehavior).
        private const string ElementDragAction = "drag";

        /// <summary>The hooks a processed command fires.</summary>
        public static List<Hook> HooksFor(WebSocketCommand webSocketCommand)
        {
            var hooks = new List<Hook>();
            if (commandsToHooks.TryGetValue(webSocketCommand.Command ?? string.Empty, out var hook))
            {
                // A turn order change fires Turn Changed only when the turn or round moved.
                var turnChanged = (webSocketCommand.Data as JObject)?["turnChanged"] is JValue { Type: JTokenType.Boolean } flag
                    && flag.Value<bool>();
                if (hook != Hook.TurnChange || turnChanged)
                    hooks.Add(hook);
            }
            if (webSocketCommand.Command == WebSocketCommandNames.ElementUpdate &&
                string.Equals(webSocketCommand.Action, ElementDragAction, StringComparison.OrdinalIgnoreCase))
                hooks.Add(Hook.ElementMove);
            return hooks;
        }
    }
}
