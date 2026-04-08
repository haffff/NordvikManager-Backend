using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    /// <summary>
    /// Shared lobby join/leave logic used by both WebSocket and WebRTC transports.
    /// </summary>
    public static class LobbyConnectionHelper
    {
        /// <summary>
        /// Adds <paramref name="connection"/> to <paramref name="lobby"/> for the given player.
        /// If the player is not yet in the lobby, fires the PlayerJoin hook and broadcasts.
        /// Returns the canonical <see cref="PlayerDTO"/> key used in <c>ConnectedPlayers</c>.
        /// </summary>
        public static async Task<PlayerDTO> ConnectPlayerAsync(
            GameLobby lobby, PlayerDTO player, IPlayerConnection connection)
        {
            if (!lobby.CheckForPlayer(player))
            {
                lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
                await lobby.ActionProcessingService.CallHookAsync(
                    Hook.PlayerJoin, new PlayerHookArgs { Player = player });
                foreach (var kv in lobby.ConnectedPlayers)
                    await kv.Value.SendMessageToPlayer(
                        new { command = WebSocketCommandNames.PlayerJoin, data = player });
                return player;
            }

            // Player already present — attach the new connection without re-firing hooks
            var canonical = lobby.ConnectedPlayers.First(kv => kv.Key.Id == player.Id).Key;
            lobby.ConnectedPlayers[canonical].Add(connection);
            return canonical;
        }

        /// <summary>
        /// Removes <paramref name="connection"/> from the lobby.
        /// When the player's last connection is removed, fires PlayerLeave and broadcasts.
        /// </summary>
        public static async Task DisconnectPlayerAsync(
            GameLobby lobby, PlayerDTO player, IPlayerConnection connection)
        {
            if (!lobby.ConnectedPlayers.ContainsKey(player))
                return;

            lobby.ConnectedPlayers[player].Remove(connection);
            if (lobby.ConnectedPlayers[player].Count > 0)
                return;

            lobby.ConnectedPlayers.Remove(player);
            await lobby.ActionProcessingService.CallHookAsync(
                Hook.PlayerLeave, new PlayerHookArgs { Player = player });
            foreach (var kv in lobby.ConnectedPlayers)
                await kv.Value.SendMessageToPlayer(
                    new { command = WebSocketCommandNames.PlayerLeave, data = player });
        }
    }
}
