using DndOnePlaceManager.Application.Commands;
using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Generic.Command
{
    public class GamePlayerCommandBase<TResponse> : CommandBase<TResponse>
    {
        public Guid GameID { get; set; }
        public PlayerDTO? Player { get; set; }
    }
}
