using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
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

        private static HashSet<string> AllowedPasstroughCommands = new HashSet<string>()
        {
            "preview_start",
            "preview_update",
            "preview_end"
        };

        public GameLobby(IServiceScopeFactory serviceScopeFactory)
        {
            serviceScope = serviceScopeFactory.CreateScope();
            
            ActionProcessingService = serviceScope.ServiceProvider.GetRequiredService<IActionProcessingService>();
            ActionProcessingService.GameLobby = this;
            ServiceScopeFactory = serviceScopeFactory;
            this.mediator = serviceScope.ServiceProvider.GetService(typeof(IMediator)) as IMediator;
            this.serviceScopeFactory = serviceScopeFactory;

            WebSockerHandlers = serviceScope.ServiceProvider.GetServices(typeof(IWebSocketHandler))?.Cast<IWebSocketHandler>().ToArray();
        }

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GameId { get; set; }
        public Dictionary<PlayerDTO, List<WebSocketManager>> ConnectedPlayers { get; set; } = new Dictionary<PlayerDTO, List<WebSocketManager>>();
        public PlayerDTO SystemPlayer { get; set; }

        public bool CheckForPlayer(PlayerDTO player)
        {
            return ConnectedPlayers.Any(x => x.Key.Id == player.Id);
        }

        public void Broadcast(object message, PlayerDTO player)
        {
            foreach (var connectedPlayer in ConnectedPlayers)
            {
                if (connectedPlayer.Key.Id != player.Id)
                {
                    connectedPlayer.Value.ForEach(async (ws) => await ws.SendMessageToPlayer(message));
                }
            }
        }

        public void SendToPlayer(object message, PlayerDTO player)
        {
            if (ConnectedPlayers.ContainsKey(player))
            {
                ConnectedPlayers[player].ForEach(async (ws) => await ws.SendMessageToPlayer(message));
            }
        }

        private async Task<WebSocketCommand> HandleWebSocketCommand(WebSocketCommand message, PlayerDTO player)
        {
            if(!CheckIfAllowed(message))
            {
                message.Command = "error";
                message.OnlyToSender = true;
                message.Result = "Not allowed";
                return message;
            }

            message.PlayerId = player.Id;
            message.GameId = GameId;

            if (AllowedPasstroughCommands.Contains(message.Command))
            {
                message.Result = "Pass";

                //Enrich

                return message;
            }

            using var scope = serviceScopeFactory.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            foreach (var item in WebSockerHandlers)
            {
                var res = await item.Handle(message, player);
                if (res != null)
                {
                    if (res != CommandResponse.Ok)
                    {
                        message.OnlyToSender = true;
                        message.Result = Enum.GetName(typeof(CommandResponse), res);
                    }

                    message.Result = "Ok";
                    break;
                }
            }

            return message;
        }

        private bool CheckIfAllowed(WebSocketCommand message)
        {
            return message.Command.ToLower() != "clientscript_execute";
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
                WebSocketCommand webSocketCommand = new WebSocketCommand()
                {
                    GameId = GameId,
                    PlayerId = player.Id,
                    Command = "error_permission",
                    Data = e.Message,
                    OnlyToSender = true
                };

                SendToPlayer(webSocketCommand, player);
                return null;
            }
            catch (WrongArgumentsException e)
            {
                WebSocketCommand webSocketCommand = new WebSocketCommand()
                {
                    GameId = GameId,
                    PlayerId = player.Id,
                    Command = "error_arguments",
                    Data = e.Message,
                    OnlyToSender = true
                };
                SendToPlayer(webSocketCommand, player);
                return null;
            }
            catch (ResourceNotFoundException e)
            {
                WebSocketCommand webSocketCommand = new WebSocketCommand()
                {
                    GameId = GameId,
                    PlayerId = player.Id,
                    Command = "error_resource",
                    Data = e.Message,
                    OnlyToSender = true
                };
                SendToPlayer(webSocketCommand, player);
                return null;
            }
            catch (Exception e)
            {
                WebSocketCommand webSocketCommand = new WebSocketCommand()
                {
                    GameId = GameId,
                    PlayerId = player.Id,
                    Command = "error_general",
                    Data = e.Message,
                    OnlyToSender = true
                };

                SendToPlayer(webSocketCommand, player);
                return null;
            }
        }


        public async Task HandleCommand(PlayerDTO player, string message)
        {
            WebSocketCommand webSocketCommand = await HandleWebSocketCommand(message, player);

            if (webSocketCommand == null)
            {
                return;
            }

            await HandlePostCommand(player, webSocketCommand);
        }

        public async Task HandleCommand(PlayerDTO player, WebSocketCommand message)
        {
            WebSocketCommand webSocketCommand = await HandleWebSocketCommand(message, player);

            await HandlePostCommand(player, webSocketCommand);
        }


        /// <summary>
        /// Handles received command
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="message"></param>
        public async Task HandlePostCommand(PlayerDTO player, WebSocketCommand webSocketCommand)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            bool result = false;
            try
            {
                if (await HandleSpecialCommands(player, webSocketCommand))
                {
                    return;
                }

                if (webSocketCommand.OnlyToSender)
                {
                    if (ConnectedPlayers.ContainsKey(player))
                    {
                        ConnectedPlayers[player].SendMessageToPlayer(webSocketCommand);
                    }
                }
                else
                {
                    if(webSocketCommand.Result == null)
                    {
                        webSocketCommand.OnlyToSender = true;
                        webSocketCommand.Result = "CommandNotFound";
                        if (ConnectedPlayers.ContainsKey(player))
                        {
                            ConnectedPlayers[player].SendMessageToPlayer(webSocketCommand);
                        }
                        return;
                    }

                    await ActionProcessingService.CommandToHook(webSocketCommand);

                    var idToCheck = webSocketCommand.Data.Type == JTokenType.Object ? webSocketCommand.Data["parentId"] ?? webSocketCommand.Data["id"] : null;
                    if (idToCheck != null && webSocketCommand.Command != "permissions_update")
                    {
                        GetPermissionsCommand permissionsCommand = new GetPermissionsCommand()
                        {
                            EntityId = webSocketCommand.Data["parentId"]?.ToGuid() ?? webSocketCommand.Data["id"].ToGuid(),
                            Player = player
                        };

                        var permissions = await mediator.Send(permissionsCommand);

                        if(permissions.Count == 0)
                        {
                            foreach (var item in ConnectedPlayers)
                            {
                                item.Value.SendMessageToPlayer(webSocketCommand);
                            }

                            return;
                        }

                        foreach (var item in ConnectedPlayers)
                        {
                            if (permissions.TryGetValue(item.Key.Id ?? Guid.Empty, out var permission) ||
                                permissions.TryGetValue(Guid.Empty, out permission))
                            {
                                if (permission.HasFlag(Permission.Read))
                                {
                                    webSocketCommand.Data["permission"] = (int)permission;
                                    item.Value.SendMessageToPlayer(webSocketCommand);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in ConnectedPlayers)
                        {
                            item.Value.SendMessageToPlayer(webSocketCommand);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }

        private async Task<bool> HandleSpecialCommands(PlayerDTO player, WebSocketCommand parsedMsg)
        {
            // PlayerList has to be returned from this place. Its unnecessary to send it to other players
            if (parsedMsg.Command == "player_list")
            {
                parsedMsg.Data = JToken.FromObject(ConnectedPlayers.Keys);
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }
            if (parsedMsg.Command == "client_loaded")
            {
                parsedMsg.OnlyToSender = true;
                await ActionProcessingService.CallHookAsync(Hook.Load, new PlayerHookArgs() { Player = player });
            }
            if (parsedMsg.Command == "debug_mode_get")
            {
                parsedMsg.Data = Debug;
                parsedMsg.OnlyToSender = true;
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }
            if (parsedMsg.Command == "debug_mode_set")
            {
                Debug = parsedMsg.Data.Value<bool>();
                parsedMsg.OnlyToSender = true;
                ConnectedPlayers[player].SendMessageToPlayer(parsedMsg);
                return true;
            }
            if (parsedMsg.Command == "execute_action")
            {
                if (parsedMsg.Data == null)
                {
                    parsedMsg.Result = "No data provided";
                    return true;
                }
                await ActionProcessingService.ExecActionAsync(parsedMsg.Data["Action"].ToString(), new HookArgs.CommandHookArgs() { Command = parsedMsg, Data = parsedMsg.Data["Args"] as JObject });
                parsedMsg.OnlyToSender = true;
                return true;
            }
            if (parsedMsg.Command == "debug_action_response" || parsedMsg.Command == "input_value")
            {
                if (parsedMsg.Data == null || parsedMsg.InputToken == null)
                {
                    parsedMsg.Result = "No data provided";
                    return true;
                }

                while (!ActionProcessingService.InputHandler.TryAdd(parsedMsg.InputToken ?? default, parsedMsg)) ;

                return true;
            }
            return false;
        }

        public void Dispose()
        {
            serviceScope.Dispose();
        }

        public IActionProcessingService ActionProcessingService { get; set; }
        public IServiceScopeFactory ServiceScopeFactory { get; }
        public bool Debug { get; internal set; }
    }
}