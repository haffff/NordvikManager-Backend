using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.TreeEntry.CheckTree
{
    public class CheckTreeCommand : GamePlayerCommandBase<CommandResponse>
    {
        public bool Fix { get; set; }
        public string EntityType { get; set; }
    }
}
