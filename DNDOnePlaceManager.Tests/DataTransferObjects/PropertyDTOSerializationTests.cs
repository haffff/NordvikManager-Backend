using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebSockets.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.Json;

namespace DNDOnePlaceManager.Tests.DataTransferObjects
{
    // Regression coverage for a casing bug that took several rounds to pin down this
    // session: PropertyDTO.ParentID is PascalCase in C#, but WebSocketCommandNames.
    // DataKeyParentId (which GameLobby's permission-filtered broadcast keys off of) is
    // the literal string "parentId". Two different serializers touch this DTO — WS
    // traffic goes through Newtonsoft (JObject.FromObject in PropertiesHandler.cs), REST
    // controllers go through System.Text.Json (Startup.cs's AddJsonOptions sets no naming
    // policy) — and neither one's *default* casing behavior produces "parentId" on its
    // own (Newtonsoft preserves "ParentID" verbatim by default; a camelCase contract
    // resolver only lowercases the leading character, giving "parentID"). The fix was
    // pinning every field with explicit [JsonProperty]/[JsonPropertyName] attributes so
    // the wire shape doesn't depend on which serializer or contract resolver happens to
    // be active at a given call site. These tests assert against the actual
    // WebSocketCommandNames constant, not a string literal, so a future rename of one
    // without the other fails loudly here instead of as a silent broadcast-filtering bug.
    public class PropertyDTOSerializationTests
    {
        private static PropertyDTO SampleDto() => new PropertyDTO
        {
            Id = Guid.NewGuid(),
            Name = "test_prop",
            ParentID = Guid.NewGuid(),
            EntityName = "GameModel",
            Value = "42",
            Permission = DndOnePlaceManager.Domain.Enums.Permission.All,
            IsProtected = true,
        };

        [Fact]
        public void NewtonsoftBareFromObject_UsesParentIdKey_MatchingWebSocketCommandNamesConstant()
        {
            // No serializer/contract resolver passed — this is exactly what
            // SetPropertyStepDefinition.cs's JObject.FromObject(property) calls do, and
            // what PropertiesHandler.cs would fall back to if its explicit
            // CamelCasePropertyNamesContractResolver were ever removed. The DTO's own
            // [JsonProperty] attribute must carry the correct casing regardless.
            var json = JObject.FromObject(SampleDto());

            Assert.True(json.ContainsKey(WebSocketCommandNames.DataKeyParentId));
            Assert.False(json.ContainsKey("ParentID"));
            Assert.False(json.ContainsKey("parentID"));
        }

        [Fact]
        public void SystemTextJson_UsesParentIdKey_MatchingWebSocketCommandNamesConstant()
        {
            // Matches Startup.cs's actual AddControllers().AddJsonOptions() config: no
            // PropertyNamingPolicy is set there, so without the DTO's own
            // [JsonPropertyName] attribute this would serialize as raw "ParentID".
            var json = JsonSerializer.Serialize(SampleDto());
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.TryGetProperty(WebSocketCommandNames.DataKeyParentId, out _));
            Assert.False(doc.RootElement.TryGetProperty("ParentID", out _));
            Assert.False(doc.RootElement.TryGetProperty("parentID", out _));
        }

        [Fact]
        public void NewtonsoftAndSystemTextJson_AgreeOnEveryFieldsCasing()
        {
            // All other fields (id/name/entityName/value/permission/isProtected) have no
            // acronym quirk, but before this session's fix REST (System.Text.Json, no
            // policy) still emitted raw PascalCase while WS (Newtonsoft, explicit
            // camelCase resolver) emitted camelCase for the very same DTO — a client
            // reading from both sources into one cache would see two different shapes
            // depending purely on which transport last wrote the entry.
            var dto = SampleDto();

            var newtonsoftKeys = JObject.FromObject(dto).Properties().Select(p => p.Name).OrderBy(k => k);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
            var stjKeys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k);

            Assert.Equal(newtonsoftKeys, stjKeys);
        }
    }
}
