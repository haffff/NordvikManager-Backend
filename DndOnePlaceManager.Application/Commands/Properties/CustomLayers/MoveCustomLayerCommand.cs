using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    // Moves an existing custom layer one step in the full stacking order (reserved
    // anchors + custom layers). The server resolves what "one step" means against
    // fresh data, not whatever the client last saw — see MoveCustomLayerCommandHandler.
    public class MoveCustomLayerCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public string ItemId { get; set; }

        // +1 = up / toward Token (higher numeric value). -1 = down / toward Map.
        public int Direction { get; set; }
    }
}
