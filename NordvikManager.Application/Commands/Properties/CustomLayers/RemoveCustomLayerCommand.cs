using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Properties
{
    // Removes one row from the "customLayers" property-list and, in the same
    // transaction, reassigns every ElementModel currently on that layer's numeric
    // id back to the default Map layer — see RemoveCustomLayerCommandHandler.
    public class RemoveCustomLayerCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public string ItemId { get; set; }
    }
}
