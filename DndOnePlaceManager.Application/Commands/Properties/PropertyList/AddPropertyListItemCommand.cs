using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    // Appends a new row to the JSON array stored in an existing PropertyModel.Value
    // (see PropertyList command handlers for the [{"id":..,"fields":{...}}] shape).
    // The target property must already exist — callers create it first via the
    // regular AddPropertyCommand (Properties.Init("[]") on the frontend).
    public class AddPropertyListItemCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid PropertyId { get; set; }
        public Dictionary<string, string?> Fields { get; set; } = new();

        // Optional client-supplied id. Roll20 sheet-worker scripts call
        // generateRowID() to pick a row id BEFORE the row exists, then create
        // it via setAttrs(repeating_x_<thatId>_field). Honoring a client id
        // (when unique) keeps that contract intact instead of forcing a
        // caller to correlate a server-generated id back to the id it
        // already handed the worker script. Omit to keep the old
        // server-generates-the-id behavior.
        public string? ItemId { get; set; }
    }
}
