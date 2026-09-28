using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class MoveElementStepData
    {
        [Description("GUID of the element (token, shape, ...) to move. E.g. %v:Data.id% in an Element hook.")]
        public string ElementId { get; set; }

        [Description("New horizontal position (canvas 'left') in pixels.")]
        public string X { get; set; }

        [Description("New vertical position (canvas 'top') in pixels.")]
        public string Y { get; set; }
    }

    public class DeleteElementStepData
    {
        [Description("GUID of the element to remove from its map.")]
        public string ElementId { get; set; }
    }

    public class ChangeMapStepData
    {
        [Description("GUID of the map to show.")]
        public string MapId { get; set; }

        [Description("GUID of the battle map view to switch. Leave empty when the game has a single battle map view.")]
        public string? BattleMapId { get; set; }
    }
}
