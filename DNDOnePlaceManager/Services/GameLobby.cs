using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    public class GameLobby : IDisposable
    {
        private IMediator mediator;
        private IServiceScopeFactory serviceScopeFactory;
        private IServiceScope serviceScope;

        public IWebSocketHandler[] WebSockerHandlers { get; }

        private static readonly HashSet<string> AllowedPassthroughCommands = new HashSet<string>()
        {
            WebSocketCommandNames.CmdPreviewStart,
            WebSocketCommandNames.CmdPreviewUpdate,
            WebSocketCommandNames.CmdPreviewEnd
        };        
        
        public GameLobby(IServiceScopeFactory serviceScopeFactory)
        {
            serviceScope = serviceScopeFactory.CreateScope();

            ActionProcessingService = serviceScope.ServiceProvider.GetRequiredService<IActionProcessingService>();
            ActionProcessingService.GameLobby = this;

            // Attach the scoped IGameEventLogger to this lobby's EventLog so that
            // application-layer handlers resolved from this scope can write to it.
            var eventLogger = serviceScope.ServiceProvider
                .GetService<DndOnePlaceManager.Application.Interfaces.IGameEventLogger>()
                as LobbyGameEventLogger;
            eventLogger?.Attach(EventLog);

            // Store in the private field only; the public property delegates to it
            this.serviceScopeFactory = serviceScopeFactory;
            this.mediator = serviceScope.ServiceProvider.GetService(typeof(IMediator)) as IMediator;

            WebSockerHandlers = serviceScope.ServiceProvider.GetServices(typeof(IWebSocketHandler))?.Cast<IWebSocketHandler>().ToArray();
        }

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GameId { get; set; }
        public Dictionary<PlayerDTO, List<IPlayerConnection>> ConnectedPlayers { get; set; } = new Dictionary<PlayerDTO, List<IPlayerConnection>>();
        public PlayerDTO SystemPlayer { get; set; }
        public IActionProcessingService ActionProcessingService { get; set; }

        // Delegates to the private field so external callers still work
        public IServiceScopeFactory ServiceScopeFactory => serviceScopeFactory;        public bool Debug { get; internal set; }

        public Implementations.GameEventLog EventLog { get; } = new();

        /// <summary>
        /// Returns an <see cref="Microsoft.Extensions.Logging.ILogger"/> whose output is
        /// written to this lobby's <see cref="EventLog"/>.  Pass the owning class type
        /// as the generic argument to get a named category automatically:
        /// <code>
        ///   var log = lobby.CreateLogger&lt;MyService&gt;();
        /// </code>
        /// </summary>
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string category)
            => new Implementations.GameEventLogLogger(EventLog, category);

        /// <inheritdoc cref="CreateLogger(string)"/>
        public Microsoft.Extensions.Logging.ILogger CreateLogger<T>()
            => CreateLogger(typeof(T).Name);

        public bool CheckForPlayer(PlayerDTO player)
        {
            return ConnectedPlayers.Any(x => x.Key.Id == player.Id);
        }

        public void Broadcast(object message, PlayerDTO player)
        {
            foreach (var connectedPlayer in ConnectedPlayers)
            {
                if (connectedPlayer.Key.Id != player.Id)
                    connectedPlayer.Value.ForEach(async ws => await ws.SendMessageToPlayer(message));
            }
        }

        public void SendToPlayer(object message, PlayerDTO player)
        {
            if (ConnectedPlayers.ContainsKey(player))
                ConnectedPlayers[player].ForEach(async ws => await ws.SendMessageToPlayer(message));
        }

        private async Task<WebSocketCommand> HandleWebSocketCommand(WebSocketCommand message, PlayerDTO player)
        {
            if (!CheckIfAllowed(message))
            {
                message.Command = WebSocketCommandNames.ErrorGeneric;
                message.OnlyToSender = true;
                message.Result = WebSocketCommandNames.ResultNotAllowed;
                return message;
            }

            message.PlayerId = player.Id;
            message.GameId = GameId;

            if (AllowedPassthroughCommands.Contains(message.Command))
            {
                message.Result = WebSocketCommandNames.ResultPass;
                return message;
            }

            using var handlerScope = serviceScopeFactory.CreateScope();
            var scopedMediator = handlerScope.ServiceProvider.GetRequiredService<IMediator>();

            foreach (var item in WebSockerHandlers)
            {
                var res = await item.Handle(message, player);
                if (res != null)
                {
                    // Bug fix: the original code set Result to the enum name then unconditionally
                    // overwrote it with "Ok" on the very next line — only set "Ok" in the else branch.
                    if (res != CommandResponse.Ok)
                    {
                        message.OnlyToSender = true;
                        message.Result = Enum.GetName(typeof(CommandResponse), res);
                    }
                    else
                    {
                        message.Result = WebSocketCommandNames.ResultOk;
                    }
                    break;
                }
            }

            return message;
        }

        // Use OrdinalIgnoreCase to avoid a string allocation from .ToLower()
        private bool CheckIfAllowed(WebSocketCommand message)
        {
            return !message.Command.Equals(WebSocketCommandNames.CmdClientScriptExecute, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<WebSocketCommand> HandleWebSocketCommand(string message, PlayerDTO player)
        {
            try
            {
                JObject parsedMsg = JObject.Parse(message);
                WebSocketCommand webSocketCommand = parsedMsg.ToObject<WebSocketCommand>();
                return await HandleWebSocketCommand(webSocketCommand, player);
            }
            catch (PermissionException e)
            {
                EventLog.Log("Error", "Permission", e.Message, player.Name);
                SendToPlayer(MakeErrorCommand(WebSocketCommandNames.ErrorPermission, e.Message, player), player);
                return null;
            }
            catch (WrongArgumentsException e)
            {
                EventLog.Log("Warning", "Command", e.Message, player.Name);
                SendToPlayer(MakeErrorCommand(WebSocketCommandNames.ErrorArguments, e.Message, player), player);
                return null;
            }
            catch (ResourceNotFoundException e)
            {
                EventLog.Log("Warning", "Command", e.Message, player.Name);
                SendToPlayer(MakeErrorCommand(WebSocketCommandNames.ErrorResource, e.Message, player), player);
                return null;
            }
            catch (Exception e)
            {
                EventLog.Log("Error", "System", e.Message, player.Name, new { exceptionType = e.GetType().Name });
                SendToPlayer(MakeErrorCommand(WebSocketCommandNames.ErrorGeneral, e.Message, player), player);
                return null;
            }
        }

        // Extracted helper — eliminates the four identical WebSocketCommand initialiser blocks
        private WebSocketCommand MakeErrorCommand(string command, string data, PlayerDTO player)
        {
            return new WebSocketCommand()
            {
                GameId = GameId,
                PlayerId = player.Id,
                Command = command,
                Data = data,
                OnlyToSender = true
            };
        }

        public async Task HandleCommand(PlayerDTO player, string message)
        {
            WebSocketCommand webSocketCommand = await HandleWebSocketCommand(message, player);
            if (webSocketCommand == null)
                return;
            await HandlePostCommand(player, webSocketCommand);
        }

        public async Task HandleCommand(PlayerDTO player, WebSocketCommand message)
        {
            WebSocketCommand webSocketCommand = await HandleWebSocketCommand(message, player);
            await HandlePostCommand(player, webSocketCommand);
        }

        /// <summary>
        /// Broadcasts or routes a processed command to the appropriate connected players.
        /// </summary>
        public async Task HandlePostCommand(PlayerDTO player, WebSocketCommand webSocketCommand)
        {
            // Renamed from `mediator` to `cmdMediator` to avoid shadowing the instance field
            using var cmdScope = serviceScopeFactory.CreateScope();
            var cmdMediator = cmdScope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                if (await HandleSpecialCommands(player, webSocketCommand))
                    return;

                if (webSocketCommand.OnlyToSender)
                {
                    if (ConnectedPlayers.ContainsKey(player))
                        ConnectedPlayers[player].SendMessageToPlayer(webSocketCommand);
                }
                else
                {
                    if (webSocketCommand.Result == null)
                    {
                        webSocketCommand.OnlyToSender = true;
                        webSocketCommand.Result = WebSocketCommandNames.ResultCommandNotFound;
                        if (ConnectedPlayers.ContainsKey(player))
                            ConnectedPlayers[player].SendMessageToPlayer(webSocketCommand);
                        return;
                    }

                    _ = Task.Run(() => ActionProcessingService.CommandToHook(webSocketCommand));

                    var idToCheck = webSocketCommand.Data.Type == JTokenType.Object
                        ? webSocketCommand.Data[WebSocketCommandNames.DataKeyParentId] ?? webSocketCommand.Data[WebSocketCommandNames.DataKeyId]
                        : null;

                    if (idToCheck != null && !webSocketCommand.Command.Equals(WebSocketCommandNames.CmdPermissionsUpdate, StringComparison.Ordinal))
                    {
                        var permissionsCommand = new GetPermissionsCommand()
                        {
                            EntityId = webSocketCommand.Data[WebSocketCommandNames.DataKeyParentId]?.ToGuid()
                                       ?? webSocketCommand.Data[WebSocketCommandNames.DataKeyId].ToGuid(),
                            Player = player
                        };

                        var permissions = await cmdMediator.Send(permissionsCommand);

                        if (permissions.Count == 0)
                        {
                            foreach (var item in ConnectedPlayers)
                                item.Value.SendMessageToPlayer(webSocketCommand);
                            return;
                        }

                        foreach (var item in ConnectedPlayers)
                        {
                            if (permissions.TryGetValue(item.Key.Id ?? Guid.Empty, out var permission) ||
                                permissions.TryGetValue(Guid.Empty, out permission))
                            {
                                if (permission.HasFlag(Permission.Read))
                                {
                                    webSocketCommand.Data[WebSocketCommandNames.DataKeyPermission] = (int)permission;
                                    item.Value.SendMessageToPlayer(webSocketCommand);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in ConnectedPlayers)
                            item.Value.SendMessageToPlayer(webSocketCommand);
                    }
                }

                // When a player is kicked, remove their in-memory ConnectedPlayers entry so that
                // a stale entry can't shadow the new player record they create when they rejoin.
                // Must happen AFTER the broadcast above so the kicked player still receives
                // the player_kick notification before being removed.
                if (webSocketCommand.Command == WebSocketCommandNames.PlayerKick
                    && webSocketCommand.Result == WebSocketCommandNames.ResultOk)
                {
                    var kickedId = webSocketCommand.Data.ToGuid();
                    var kickedEntry = ConnectedPlayers.Keys.FirstOrDefault(p => p.Id == kickedId);
                    if (kickedEntry != null)
                        ConnectedPlayers.Remove(kickedEntry);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }

        private async Task<bool> HandleSpecialCommands(PlayerDTO player, WebSocketCommand parsedMsg)
        {
            // PlayerList must be returned only to the requester — not broadcast
            if (parsedMsg.Command == WebSocketCommandNames.CmdPlayerList)
            {
                parsedMsg.Data = JToken.FromObject(ConnectedPlayers.Keys);
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }
            if (parsedMsg.Command == WebSocketCommandNames.CmdClientLoaded)
            {
                parsedMsg.OnlyToSender = true;
                // Intentional fall-through: command continues to normal dispatch
            }

            if (parsedMsg.Command == WebSocketCommandNames.CmdClientLayoutReady)
            {
                // Layout helper is fully initialised on the client — safe to show views now
                _ = Task.Run(() => ActionProcessingService.CallHookAsync(Hook.Load, new PlayerHookArgs() { Player = player }));
                parsedMsg.OnlyToSender = true;
                return true;
            }

            if (parsedMsg.Command == WebSocketCommandNames.CmdDebugModeGet)
            {
                parsedMsg.Data = Debug;
                parsedMsg.OnlyToSender = true;
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }

            if (parsedMsg.Command == WebSocketCommandNames.CmdDebugModeSet)
            {
                Debug = parsedMsg.Data.Value<bool>();
                parsedMsg.OnlyToSender = true;
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }

            if (parsedMsg.Command == WebSocketCommandNames.CmdExecuteAction)
            {
                if (parsedMsg.Data == null)
                {
                    parsedMsg.Result = WebSocketCommandNames.ResultNoData;
                    return true;
                }

                var actionName = parsedMsg.Data[WebSocketCommandNames.DataKeyAction]?.ToString();
                var argsToken = parsedMsg.Data[WebSocketCommandNames.DataKeyArgs];
                var sharedVariables = argsToken is JObject argsObj
                    ? argsObj.ToObject<Dictionary<string, object>>()
                    : null;

                _ = Task.Run(() => ActionProcessingService.ExecActionAsync(
                    actionName,
                    new HookArgs.CommandHookArgs() { Command = parsedMsg, Data = argsToken as JObject, Player = player },
                    sharedVariables));

                parsedMsg.OnlyToSender = true;
                parsedMsg.Result = WebSocketCommandNames.ResultOk;
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);

                return true;
            }

            if (parsedMsg.Command == WebSocketCommandNames.CmdDebugActionResponse ||
                parsedMsg.Command == WebSocketCommandNames.CmdInputValue)
            {
                if (parsedMsg.Data == null || parsedMsg.InputToken == null)
                {
                    parsedMsg.Result = WebSocketCommandNames.ResultNoData;
                    return true;
                }
                if (ActionProcessingService.InputHandler.TryRemove(parsedMsg.InputToken.Value, out var tcs))
                    tcs.TrySetResult(parsedMsg);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            serviceScope.Dispose();
        }
    }
}