using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    // Creates (lazily, if missing) the "customLayers" property-list, then inserts one
    // new named layer with a freshly allocated numeric layer id — see
    // AddCustomLayerCommandHandler and CustomLayerLayout.
    public class AddCustomLayerCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public string Name { get; set; }

        // Where to insert the new layer, relative to a reference value the client
        // currently sees — either a reserved anchor (CustomLayerLayout.MapLayerId
        // etc.) or another custom layer's current layerId. Resolved against fresh
        // server-side data, not the client's snapshot, so it stays correct even if a
        // concurrent Add/Move/Remove shifted things since the client last fetched.
        // Null means "insert at the very top of the stack" (the old part-1 default).
        public int? AfterLayerId { get; set; }
    }
}
