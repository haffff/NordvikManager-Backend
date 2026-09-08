using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Exceptions;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    // Shared by the AddPropertyListItem/RemovePropertyListItem/UpdatePropertyListItem
    // step definitions — resolving the target list property and parsing the JSON
    // "Fields" step-data field is identical across all three.
    internal static class PropertyListStepHelper
    {
        // Matches PropertiesHandler.cs's _camelSerializer — the wire contract every
        // property_update consumer (PropertiesManager, CardAPI) expects.
        public static readonly JsonSerializer CamelSerializer = new JsonSerializer
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        public static async Task<Guid> ResolvePropertyIdAsync(IMediator mediator, GameLobby gameLobby, Guid parentId, string propertyName, string stepName)
        {
            var result = await mediator.Send(new GetPropertiesByQueryCommand
            {
                Player = gameLobby.SystemPlayer,
                ParentIDs = new[] { parentId },
                PropertyNames = new[] { propertyName },
            });

            var property = result?.FirstOrDefault();
            if (property?.Id == null)
                throw new ActionProcessException(
                    $"{stepName}: list property '{propertyName}' not found on entity '{parentId}' — create it first with a Set Property step (Value: \"[]\").");

            return property.Id.Value;
        }

        public static Dictionary<string, string?> ParseFields(string? json, string stepName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string?>();

            try
            {
                return JObject.Parse(json).ToObject<Dictionary<string, string?>>() ?? new Dictionary<string, string?>();
            }
            catch (JsonException ex)
            {
                throw new ActionProcessException($"{stepName}: 'Fields' is not valid JSON — {ex.Message}");
            }
        }
    }
}
