using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets
{
    public class WebSocketManager : IWebSocketManager, IDisposable
    {
        static private readonly Dictionary<Guid, GameLobby> games = new Dictionary<Guid, GameLobby>();
        GameLobby lobby;
        PlayerDTO player;
        User user;
        Guid gameId;
        private byte[] buffer = new byte[1024];
        private HttpContext httpContext;
        private readonly IServiceScope scope;
        private readonly IMediator mediator;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private WebSocket ws = null;

        public WebSocketManager(IServiceScopeFactory serviceScopeFactory, IWebSocketTokenValidator webSocketTokenValidator)
        {
            scope = serviceScopeFactory.CreateScope();
            mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            this.serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Handle(HttpContext httpContext)
        {
            this.httpContext = httpContext;
            using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            ws = webSocket;

            if (!await HandleLobbyJoining(webSocket))
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Joining lobby failed", CancellationToken.None);
                return;
            }

            await webSocket.SendText("OK");
            await HandleReceivingMessages(webSocket);
        }

        private async Task<bool> HandleReceivingMessages(WebSocket webSocket)
        {
            WebSocketReceiveResult result = null;
            do
            {
                (string message, WebSocketReceiveResult loopResult) = await ReceiveMessage(webSocket);
                result = loopResult;

                await lobby.HandleCommand(player, message);
            } while (!result.CloseStatus.HasValue);

            lobby.ConnectedPlayers[player].Remove(this);

            if (lobby.ConnectedPlayers[player].Count == 0)
            {
                lobby.ConnectedPlayers.Remove(player);

                await lobby.ActionProcessingService.CallHookAsync(Hook.PlayerLeave, new PlayerHookArgs { Player = player });

                foreach (var item in lobby.ConnectedPlayers)
                {
                    item.Value.SendMessageToPlayer(new { command = "player_leave", data = player });
                }
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            return true;
        }

        private async Task<(string, WebSocketReceiveResult)> ReceiveMessage(WebSocket webSocket)
        {
            WebSocketReceiveResult result = null;
            string message = string.Empty;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                message += Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = 0;
            } while (!result.EndOfMessage);


            return (message, result);
        }

        public async Task<bool> SendMessageToPlayer(object message)
        {
            ws.SendObject(message);
            return true;
        }

        public static void SendCommandToUser(User user, WebSocketCommand command)
        {
            var clients = games.Values.SelectMany(x => x.ConnectedPlayers.SelectMany(y => y.Value.Where(z => z.user.Id == user.Id)));
            foreach (var item in clients)
            {
                item.SendMessageToPlayer(command);
            }
        }

        public static void SendCommandToLobby(WebSocketCommand command, PlayerDTO[] players = null)
        {
            if (command.GameId == null)
            {
                return;
            }

            var game = games[command.GameId.Value];

            if (players == null)
            {
                if (command.PlayerId != null)
                {
                    var player = game.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == command.PlayerId);
                    players = [ player ];
                }
                else
                {
                    players = game.ConnectedPlayers.Keys?.ToArray() ?? [];
                }
            }

            foreach (var item in players)
            {
                if (game.ConnectedPlayers.ContainsKey(item))
                {
                    game.ConnectedPlayers[item].ForEach(async (ws) => await ws.SendMessageToPlayer(command));
                }
            }
        }

        public async Task<bool> ExecuteActionInGameLobby(ActionDto action, Guid gameId)
        {
            await games[gameId].ActionProcessingService.ExecActionAsync(action, new BackEndHookArgs() { GameId = gameId });
            return true;
        }

        public async Task<bool> SendLogInformation(string message, string code, LogLevel logType, Guid gameId, Guid? id)
        {
            var game = games[gameId];
            var playerClients = game.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == id);
            if (playerClients != null)
            {
                foreach (var item in game.ConnectedPlayers[playerClients])
                {
                    item.SendMessageToPlayer(new { command = "log", data = new { message, code, logType } });
                }
            }
            return true;
        }

        private async Task<bool> HandleLobbyJoining(WebSocket webSocket)
        {
            try
            {
                user = httpContext.Items["User"] as User;
                if (user == null)
                {
                    return false;
                }

                (string gameIdStr, _) = await ReceiveMessage(webSocket);

                gameId = Guid.Parse(gameIdStr);

                var response = await mediator.Send(new GetPlayerCommand() { GameID = gameId, User = user });

                if (response.Player == null)
                    return false;

                player = response.Player;

                if (!games.ContainsKey(gameId))
                {
                    GetSystemPlayerCommand getSystemPlayerCommand = new GetSystemPlayerCommand() { GameID = gameId };
                    var systemPlayer = await mediator.Send(getSystemPlayerCommand);

                    games.Add(gameId, new GameLobby(serviceScopeFactory) { GameId = gameId, SystemPlayer = systemPlayer, ConnectedPlayers = new Dictionary<PlayerDTO, List<WebSocketManager>>() });
                }

                lobby = games[gameId];
                await AddPlayerAndAssingWebsocket();

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private async Task AddPlayerAndAssingWebsocket()
        {
            if (!lobby.CheckForPlayer(player))
            {
                lobby.ConnectedPlayers.Add(player, new List<WebSocketManager>() { this });

                await lobby.ActionProcessingService.CallHookAsync(Hook.PlayerJoin, new PlayerHookArgs { Player = player });

                foreach (var item in lobby.ConnectedPlayers)
                {
                    item.Value.SendMessageToPlayer(new { command = "player_join", data = player });
                }
            }
            else
            {
                player = lobby.ConnectedPlayers.First(x => x.Key.Id == player.Id).Key;
                lobby.ConnectedPlayers[player].Add(this);
            }
        }

        public async Task HandleCommandInLobby(Guid? gameId, WebSocketCommand command, PlayerDTO player)
        {
            if (games.ContainsKey(gameId.Value))
            {
                var gameLobby = games[gameId.Value];
                await gameLobby.HandleCommand(player, command);
            }
        }


        public void Dispose()
        {
            if (lobby?.ConnectedPlayers?.ContainsKey(player) == true)
            {
                if (lobby.ConnectedPlayers[player].Contains(this))
                {
                    lobby.ConnectedPlayers[player].Remove(this);
                }

                if (lobby.ConnectedPlayers[player].Count == 0)
                {
                    lobby.ConnectedPlayers.Remove(player);
                }
            }

            if (lobby?.ConnectedPlayers?.Count == 0)
            {
                games.Remove(gameId);
            }

            scope?.Dispose();
            ws?.Dispose();
        }
    }
}
