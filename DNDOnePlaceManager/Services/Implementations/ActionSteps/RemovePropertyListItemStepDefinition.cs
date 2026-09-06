using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class RemovePropertyListItemStepDefinition : IActionStepDefinition
    {
        public string Name => "Remove Property List Item";
        public string Value => "RemovePropertyListItem";
        public string Category => "Properties";
        public string Description => "Removes a row from a list-typed property by its id.";
        public Type DataType => typeof(RemovePropertyListItemStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RemovePropertyListItemStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ParentId))
                throw new ActionProcessException("RemovePropertyListItem: 'ParentId' is required.");
            if (string.IsNullOrWhiteSpace(stepData.PropertyName))
                throw new ActionProcessException("RemovePropertyListItem: 'PropertyName' is required.");
            if (string.IsNullOrWhiteSpace(stepData.ItemId))
                throw new ActionProcessException("RemovePropertyListItem: 'ItemId' is required.");
            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
                throw new ActionProcessException($"RemovePropertyListItem: 'ParentId' value '{stepData.ParentId}' is not a valid GUID.");

            var propertyId = await PropertyListStepHelper.ResolvePropertyIdAsync(mediator, gameLobby, parentGuid, stepData.PropertyName, "RemovePropertyListItem");

            // Routed through gameLobby.HandleCommand (same WS command PropertiesHandler
            // handles) rather than a direct mediator.Send — no id needs to come back out,
            // so this gets the live broadcast for free instead of building one by hand.
            await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand
            {
                Command = WebSocketCommandNames.PropertyListItemRemove,
                Data = JObject.FromObject(new { propertyId, itemId = stepData.ItemId }),
            });
        }
    }
}
