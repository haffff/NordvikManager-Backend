using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Game.DeleteGame
{
    public class RemoveGameCommand : GamePlayerCommandBase<CommandResponse>
    {
        /// <summary>The server's local admin, who may delete any game without being in it.</summary>
        public bool AsLocalAdmin { get; set; }
    }
}
