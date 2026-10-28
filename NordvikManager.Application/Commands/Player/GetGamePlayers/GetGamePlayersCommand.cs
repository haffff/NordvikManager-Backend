using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.Player.GetGamePlayers
{
    /// <summary>Every (non-system) player of a game, connected or not, with the GM marked by IsOwner.</summary>
    public class GetGamePlayersCommand : CommandBase<List<PlayerDTO>>
    {
        public Guid GameId { get; set; }
    }
}
