using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry
{
    public class ConnectTreeEntriesCommand : GamePlayerCommandBase<CommandResponse>
    {
        public string EntityType { get; set; }
    }
}
