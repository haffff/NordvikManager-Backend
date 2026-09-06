using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    // Serialized form of a single row inside a PropertyModel.Value JSON array
    // (see PropertyList command handlers) — pinned field names so the stored JSON
    // stays stable regardless of which serializer settings are active elsewhere.
    public class PropertyListItemDTO
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("fields")]
        public Dictionary<string, string?> Fields { get; set; } = new();
    }
}
