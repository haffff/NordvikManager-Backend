using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
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
using System;
using System.Collections.Concurrent;
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
        // ── Constants ───────────────────────────────────────────────────────
        private const int ReceiveBufferSize = 1024;
        // ── Static lobby registry (shared across all connections) ───────────
        private static readonly ConcurrentDictionary<Guid, GameLobby> games = new ConcurrentDictionary<Guid, GameLobby>();
        // ── Per-connection state ────────────────────────────────────────────
        private GameLobby lobby;
        private PlayerDTO player;
        private User user;
        private Guid gameId;
        private readonly byte[] buffer = new byte[ReceiveBufferSize];
        private HttpContext httpContext;
        private WebSocket ws;
        private readonly IServiceScope scope;
        private readonly IMediator mediator;
        private readonly IServiceScopeFactory serviceScopeFactory;

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
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.ProtocolError,
                    WebSocketCommandNames.LobbyJoinFailedReason,
                    CancellationToken.None);
                return;
            }
            await webSocket.SendText(WebSocketCommandNames.HandshakeOk);
            await HandleReceivingMessages(webSocket);
        }

        public async Task<bool> SendMessageToPlayer(object message)
        {
            if (ws != null)
                await ws.SendObject(message);
            return true;
        }

        public async Task HandleCommandInLobby(Guid? gameId, WebSocketCommand command, PlayerDTO player)
        {
            if (gameId == null)
                return;
            if (games.TryGetValue(gameId.Value, out var gameLobby))
                await gameLobby.HandleCommand(player, command);
        }

        public GameLobby GetLobby(Guid gameId)
        {
            games.TryGetValue(gameId, out var lobby);
            return lobby;
        }

        public async Task<bool> ExecuteActionInGameLobby(ActionDto action, Guid gameId)
        {
            await games[gameId].ActionProcessingService.ExecActionAsync(
                action, new BackEndHookArgs { GameId = gameId });
            return true;
        }

        public async Task<bool> SendLogInformation(string message, string code, LogLevel logType, Guid gameId, Guid? id)
        {
            var game = games[gameId];
            var playerClients = game.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == id);
            if (playerClients != null)
            {
                foreach (var item in game.ConnectedPlayers[playerClients])
                    await item.SendMessageToPlayer(
                        new { command = WebSocketCommandNames.LogCommand, data = new { message, code, logType } });
            }
            return true;
        }

        // Static broadcast helpers 
        public static void SendCommandToUser(User user, WebSocketCommand command)
        {
            var clients = games.Values
                .SelectMany(g => g.ConnectedPlayers
                    .SelectMany(kv => kv.Value.Where(wsm => wsm.user.Id == user.Id)));
            foreach (var client in clients)
                _ = client.SendMessageToPlayer(command);
        }

        public static void SendCommandToLobby(WebSocketCommand command, PlayerDTO[] players = null)
        {
            if (command.GameId == null)
                return;
            if (!games.TryGetValue(command.GameId.Value, out var game))
                return;
            if (players == null)
            {
                players = command.PlayerId != null
                    ? new[] { game.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == command.PlayerId) }
                    : game.ConnectedPlayers.Keys.ToArray();
            }
            foreach (var p in players)
            {
                if (game.ConnectedPlayers.TryGetValue(p, out var connections))
                    connections.ForEach(async wsm => await wsm.SendMessageToPlayer(command));
            }
        }

        // Receive loop 
        private async Task HandleReceivingMessages(WebSocket webSocket)
        {
            WebSocketReceiveResult result;
            do
            {
                string message;
                (message, result) = await ReceiveMessage(webSocket);
                await lobby.HandleCommand(player, message);
            }
            while (!result.CloseStatus.HasValue);
            await OnPlayerDisconnected();
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        private async Task<(string message, WebSocketReceiveResult result)> ReceiveMessage(WebSocket webSocket)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                Array.Clear(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
            return (sb.ToString(), result);
        }

        private async Task<bool> HandleLobbyJoining(WebSocket webSocket)
        {
            try
            {
                user = httpContext.Items[WebSocketCommandNames.UserContextKey] as User;
                if (user == null)
                    return false;
                (string gameIdStr, WebSocketReceiveResult _) = await ReceiveMessage(webSocket);
                gameId = Guid.Parse(gameIdStr);
                var response = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
                if (response.Player == null)
                    return false;
                player = response.Player;
                lobby = games.GetOrAdd(gameId, _ =>
                {
                    var systemPlayer = mediator.Send(new GetSystemPlayerCommand { GameID = gameId }).GetAwaiter().GetResult();
                    return new GameLobby(serviceScopeFactory)
                    {
                        GameId = gameId,
                        SystemPlayer = systemPlayer,
                        ConnectedPlayers = new Dictionary<PlayerDTO, List<WebSocketManager>>()
                    };
                });
                await AddPlayerAndAssignWebSocket();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private async Task AddPlayerAndAssignWebSocket()
        {
            if (!lobby.CheckForPlayer(player))
            {
                lobby.ConnectedPlayers[player] = new List<WebSocketManager> { this };
                await lobby.ActionProcessingService.CallHookAsync(
                    Hook.PlayerJoin, new PlayerHookArgs { Player = player });
                foreach (var kv in lobby.ConnectedPlayers)
                    await kv.Value.SendMessageToPlayer(
                        new { command = WebSocketCommandNames.PlayerJoin, data = player });
            }
            else
            {
                player = lobby.ConnectedPlayers.First(kv => kv.Key.Id == player.Id).Key;
                lobby.ConnectedPlayers[player].Add(this);
            }
        }

        private async Task OnPlayerDisconnected()
        {
            lobby.ConnectedPlayers[player].Remove(this);
            if (lobby.ConnectedPlayers[player].Count > 0)
                return;
            lobby.ConnectedPlayers.Remove(player);
            await lobby.ActionProcessingService.CallHookAsync(
                Hook.PlayerLeave, new PlayerHookArgs { Player = player });
            foreach (var kv in lobby.ConnectedPlayers)
                await kv.Value.SendMessageToPlayer(
                    new { command = WebSocketCommandNames.PlayerLeave, data = player });
        }

        // ── IDisposable ─────────────────────────────────────────────────────
        public void Dispose()
        {
            if (lobby != null && lobby.ConnectedPlayers != null && lobby.ConnectedPlayers.ContainsKey(player))
            {
                lobby.ConnectedPlayers[player].Remove(this);
                if (lobby.ConnectedPlayers[player].Count == 0)
                    lobby.ConnectedPlayers.Remove(player);
            }
            if (lobby != null && lobby.ConnectedPlayers != null && lobby.ConnectedPlayers.Count == 0)
                games.TryRemove(gameId, out _);
            scope?.Dispose();
            ws?.Dispose();
        }
    }
}
