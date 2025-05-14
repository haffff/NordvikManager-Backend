using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree
{
    public class CheckTreeCommand : GamePlayerCommandBase<CommandResponse>
    {
        public bool Fix { get; set; }
        public string EntityType { get; set; }
    }
}
