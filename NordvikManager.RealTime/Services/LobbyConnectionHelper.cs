using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Enums;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Extensions;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services
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
            var canonical = lobby.ConnectedPlayers.Attach(player, connection, out var isNewPlayer);
            if (isNewPlayer)
            {
                // Catch up any addon whose Install hook never ran (e.g. auto-installed at
                // game creation, before this lobby existed) before PlayerJoin — anything
                // that join-time logic expects an addon to have set up should be there.
                await lobby.ActionProcessingService.RunPendingAddonInstallHooksAsync();
                await lobby.ActionProcessingService.CallHookAsync(
                    Hook.PlayerJoin, new PlayerHookArgs { Player = player });
                foreach (var kv in lobby.ConnectedPlayers)
                    await kv.Value.SendMessageToPlayer(
                        new { command = WebSocketCommandNames.PlayerJoin, data = player });
                return canonical;
            }

            // Player already present: the new connection joins them without re-firing hooks.
            return canonical;
        }

        /// <summary>
        /// Removes <paramref name="connection"/> from the lobby.
        /// When the player's last connection is removed, fires PlayerLeave and broadcasts.
        /// </summary>
        public static async Task DisconnectPlayerAsync(
            GameLobby lobby, PlayerDTO player, IPlayerConnection connection)
        {
            if (!lobby.ConnectedPlayers.Detach(player, connection))
                return;

            await lobby.ActionProcessingService.CallHookAsync(
                Hook.PlayerLeave, new PlayerHookArgs { Player = player });
            foreach (var kv in lobby.ConnectedPlayers)
                await kv.Value.SendMessageToPlayer(
                    new { command = WebSocketCommandNames.PlayerLeave, data = player });
        }
    }
}
