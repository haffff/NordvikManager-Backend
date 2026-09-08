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
    public class UpdatePropertyListItemStepDefinition : IActionStepDefinition
    {
        public string Name => "Update Property List Item";
        public string Value => "UpdatePropertyListItem";
        public string Category => "Properties";
        public string Description => "Merges fields into an existing row of a list-typed property, identified by its id.";
        public Type DataType => typeof(UpdatePropertyListItemStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<UpdatePropertyListItemStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ParentId))
                throw new ActionProcessException("UpdatePropertyListItem: 'ParentId' is required.");
            if (string.IsNullOrWhiteSpace(stepData.PropertyName))
                throw new ActionProcessException("UpdatePropertyListItem: 'PropertyName' is required.");
            if (string.IsNullOrWhiteSpace(stepData.ItemId))
                throw new ActionProcessException("UpdatePropertyListItem: 'ItemId' is required.");
            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
                throw new ActionProcessException($"UpdatePropertyListItem: 'ParentId' value '{stepData.ParentId}' is not a valid GUID.");

            var propertyId = await PropertyListStepHelper.ResolvePropertyIdAsync(mediator, gameLobby, parentGuid, stepData.PropertyName, "UpdatePropertyListItem");
            var fields = PropertyListStepHelper.ParseFields(stepData.Fields, "UpdatePropertyListItem");

            await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand
            {
                Command = WebSocketCommandNames.PropertyListItemUpdate,
                Data = JObject.FromObject(new { propertyId, itemId = stepData.ItemId, fields }),
            });
        }
    }
}
