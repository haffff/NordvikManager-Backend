using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.TreeEntry.ConnectTreeEntry
{
    public class ConnectTreeEntriesCommand : GamePlayerCommandBase<CommandResponse>
    {
        public string EntityType { get; set; }
    }
}
