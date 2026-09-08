using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    // (De)serializes the JSON array stored in PropertyModel.Value for list-typed
    // properties — [{"id":"a1","fields":{"name":"Hunt","value":"2"}}, ...].
    internal static class PropertyListJson
    {
        public static List<PropertyListItemDTO> Deserialize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? new List<PropertyListItemDTO>()
                : JsonConvert.DeserializeObject<List<PropertyListItemDTO>>(value) ?? new List<PropertyListItemDTO>();

        public static string Serialize(List<PropertyListItemDTO> items) =>
            JsonConvert.SerializeObject(items);
    }
}
