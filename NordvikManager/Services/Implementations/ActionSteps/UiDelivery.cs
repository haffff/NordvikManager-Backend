using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.WebSockets;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Sends a menu item, toolbar button or map tool to the players who should have it:
    /// everyone; only the player who triggered the action (OnlyTriggeringPlayer); only GMs,
    /// the owner and players with Edit on the game (GmOnly); or with both, the triggering
    /// player if they are a GM.
    /// </summary>
    public static class UiDelivery
    {
        public static async Task SendAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object> variables,
            string step, WebSocketCommand command, bool onlyTriggeringPlayer, bool gmOnly)
        {
            IEnumerable<PlayerDTO> to = gameLobby.ConnectedPlayers.Keys;

            if (onlyTriggeringPlayer)
            {
                variables.TryGetValue("Player", out var playerObj);
                var triggering = playerObj as PlayerDTO
                    ?? throw new ActionProcessException($"{step}: OnlyTriggeringPlayer is set but no player triggered this action.");
                to = to.Where(x => x.Id == triggering.Id).ToList();
                if (!to.Any())
                    throw new ActionProcessException($"{step}: the player who triggered this action is not connected.");
            }

            if (!onlyTriggeringPlayer && !gmOnly)
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
                return;
            }

            foreach (var player in to.ToList())
                if (!gmOnly || await IsGmAsync(mediator, gameLobby, player))
                    gameLobby.SendToPlayer(command, player);
        }

        /// <summary>The game's owner, or a player with Edit on the game (a co-GM).</summary>
        public static async Task<bool> IsGmAsync(IMediator mediator, GameLobby gameLobby, PlayerDTO player) =>
            player.IsOwner == true || await mediator.Send(new CheckPermissionsCommand
            {
                Player = player,
                EntityId = gameLobby.GameId,
                RequiredPermission = Permission.Edit,
            });
    }
}
