using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class UpdatePropertyListItemCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid PropertyId { get; set; }
        public string ItemId { get; set; }

        // Merged into the existing row's fields (only the supplied keys change) —
        // mirrors Roll20 setAttrs' partial-update semantics.
        public Dictionary<string, string?> Fields { get; set; } = new();
    }
}
