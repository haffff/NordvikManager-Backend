using DndOnePlaceManager.Domain.Enums;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    // Every field below is pinned to an explicit camelCase wire name. Two serializers
    // touch this DTO — WS traffic goes through Newtonsoft (JObject.FromObject in
    // PropertiesHandler.cs, via an explicit CamelCasePropertyNamesContractResolver),
    // while REST controllers serialize via System.Text.Json with no naming policy
    // configured (see AddJsonOptions in Startup.cs — there's no AddNewtonsoftJson call
    // anywhere in this backend), so left alone REST would emit raw PascalCase
    // (Id/Name/Value/...) while WS emits camelCase for the same DTO. Pinning every
    // field here makes both paths agree regardless of which serializer or contract
    // resolver is active at a given call site.
    public class PropertyDTO : IGameDataTransferObject
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // WebSocketCommandNames.DataKeyParentId ("parentId") is what GameLobby's
        // permission-filtered broadcast reads to find the owning entity for a
        // property_add/update/remove message. CamelCasePropertyNamesContractResolver
        // only lowercases the leading character (ParentID -> parentID, "ID" stays as
        // an acronym), which doesn't match that constant, so this one needs pinning
        // for correctness beyond just REST/WS consistency.
        [JsonProperty("parentId")]
        [JsonPropertyName("parentId")]
        public Guid? ParentID { get; set; }

        [JsonProperty("entityName")]
        [JsonPropertyName("entityName")]
        public string? EntityName { get; set; }

        [JsonProperty("value")]
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonProperty("permission")]
        [JsonPropertyName("permission")]
        public Permission? Permission { get; set; }

        [JsonProperty("isProtected")]
        [JsonPropertyName("isProtected")]
        public bool IsProtected { get; set; }
    }
}
