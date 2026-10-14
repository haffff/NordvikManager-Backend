using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Player.GetGamePlayers
{
    /// <summary>Every (non-system) player of a game, connected or not, with the GM marked by IsOwner.</summary>
    public class GetGamePlayersCommand : CommandBase<List<PlayerDTO>>
    {
        public Guid GameId { get; set; }
    }
}
