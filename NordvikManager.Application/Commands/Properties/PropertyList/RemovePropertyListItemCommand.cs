using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Properties
{
    public class RemovePropertyListItemCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid PropertyId { get; set; }
        public string ItemId { get; set; }
    }
}
