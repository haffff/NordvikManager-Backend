using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
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

        private readonly Dictionary<string, Hook> commandsToHooks = new Dictionary<string, Hook>()
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
            { WebSocketCommandNames.CardDelete,      Hook.CardDelete }
        };

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

        public async Task CommandToHook(WebSocketCommand webSocketCommand)
        {
            try
            {
                if (commandsToHooks.TryGetValue(webSocketCommand.Command, out var hook))
                {
                    var player = webSocketCommand.PlayerId.HasValue
                        ? GameLobby.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == webSocketCommand.PlayerId)
                        : null;
                    await CallHookAsync(hook, new HookArgs.CommandHookArgs()
                    {
                        Command = webSocketCommand,
                        Player = player,
                        Data = webSocketCommand.Data as JObject,
                    });
                }
            }
            catch (Exception e)
            {
                GameLobby?.EventLog?.Log("Error", "Action", $"CommandToHook for '{webSocketCommand.Command}' failed: {e.Message}",
                    details: new { command = webSocketCommand.Command, exceptionType = e.GetType().Name });
            }
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
        public async Task ExecActionAsync(ActionDto action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null, IMediator mediator = null)
        {
            // Always create a scope so we can get dbContext for the property query resolver.
            // If mediator was already provided by a caller, we still need our own scope for dbContext.
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            mediator ??= scope.ServiceProvider.GetRequiredService<IMediator>();

            var entry = new ActionRunEntry { ActionName = action.Name };
            RunningActions[entry.RunId] = entry;

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
                    entry.CancellationToken.ThrowIfCancellationRequested();

                    var debugCmd = await DebugLog(mediator, DebugLogData.ExecutingStep(action, step, variables), entry: entry);

                    if (debugCmd != null && debugCmd.Data.ToString() == WebSocketCommandNames.DebugStopSignal)
                    {
                        entry.SetCompleted();
                        return;
                    }

                    if (!actionStepDefinitions.TryGetValue(step[WebSocketCommandNames.StepTypeKey].ToString(), out var stepDefinition))
                    {
                        if (step[WebSocketCommandNames.StepTypeKey]?.ToString() == WebSocketCommandNames.StepTypeExit)
                        {
                            entry.SetCompleted();
                            return;
                        }
                        await DebugLog(mediator, DebugLogData.StepNotFound(step), false, entry: entry);
                        continue;
                    }
                    else
                    {
                        var stepObject = step as JObject;
                        var stepType = step[WebSocketCommandNames.StepTypeKey]?.ToString();
                        entry.SetStep(stepType);

                        // Resolve %q:% / %qn:% query patterns first, then standard %varName% substitution.
                        // Skip "DefaultValue" tokens — those are resolved lazily inside the step definition
                        // after Value has been evaluated, so a %var% default isn't erased by an empty variable.
                        var resolver = new ActionPropertyQueryResolver(dbContext, GameLobby.GameId);
                        foreach (var token in stepObject.Descendants().OfType<JValue>())
                        {
                            if (token.Parent is JProperty jp && jp.Name == "DefaultValue")
                                continue;

                            var raw = token.Value?.ToString() ?? string.Empty;
                            raw = await resolver.PreResolveQueriesAsync(raw, variables);
                            token.Value = raw.Prepare(variables);
                        }

                        await stepDefinition.Execute(mediator, variables, GameLobby, step.ToObject<ActionStep>());
                    }
                }
                await DebugLog(mediator, DebugLogData.Finishing(action, steps, variables), entry: entry);
                entry.SetCompleted();
            }
            catch (OperationCanceledException)
            {
                // entry.Kill() already set State = Killed; nothing more to do
            }
            catch (Exception e)
            {
                entry.SetFaulted(e.Message);
                GameLobby?.EventLog?.Log("Error", "Action", $"Action '{action.Name}' failed: {e.Message}",
                    details: new { actionName = action.Name, actionId = action.Id, lastStep = entry.CurrentStep, exceptionType = e.GetType().Name });
                await DebugLog(mediator, DebugLogData.Fault(e.Message), false, entry: entry);
            }
            finally
            {
                // Keep the entry briefly so callers can observe terminal state, then remove it
                _ = Task.Delay(TimeSpan.FromSeconds(30))
                        .ContinueWith(__ => RunningActions.TryRemove(entry.RunId, out _));
            }
        }

        public async Task ExecActionAsync(string action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

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
                return;

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
                    return;
                }
            }

            await ExecActionAsync(foundActionDto, hookArg, sharedVariables, mediator);
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
}
