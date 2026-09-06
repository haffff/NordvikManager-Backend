using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class AddPropertyListItemStepDefinition : IActionStepDefinition
    {
        public string Name => "Add Property List Item";
        public string Value => "AddPropertyListItem";
        public string Category => "Properties";
        public string Description => "Appends a new row to a list-typed property (the property must already exist).";
        public Type DataType => typeof(AddPropertyListItemStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<AddPropertyListItemStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ParentId))
                throw new ActionProcessException("AddPropertyListItem: 'ParentId' is required.");
            if (string.IsNullOrWhiteSpace(stepData.PropertyName))
                throw new ActionProcessException("AddPropertyListItem: 'PropertyName' is required.");
            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
                throw new ActionProcessException($"AddPropertyListItem: 'ParentId' value '{stepData.ParentId}' is not a valid GUID.");

            var propertyId = await PropertyListStepHelper.ResolvePropertyIdAsync(mediator, gameLobby, parentGuid, stepData.PropertyName, "AddPropertyListItem");
            var fields = PropertyListStepHelper.ParseFields(stepData.Fields, "AddPropertyListItem");

            // Dispatched directly (not via gameLobby.HandleCommand) so the new row's
            // generated id is available synchronously for Output — mirrors
            // CreateCardStepDefinition, which needs the same thing for its own Output.
            var (_, dto) = await mediator.Send(new AddPropertyListItemCommand
            {
                Player = gameLobby.SystemPlayer,
                PropertyId = propertyId,
                Fields = fields,
            });

            // Broadcast via HandlePostCommand (not gameLobby.Broadcast) so this still goes
            // through the per-connected-player Read-permission filter on the property's
            // parentId — gameLobby.Broadcast sends to every connected player unconditionally,
            // which would leak a player-scoped list's contents to the whole lobby.
            await gameLobby.HandlePostCommand(gameLobby.SystemPlayer, new WebSockets.WebSocketCommand
            {
                Command = WebSockets.Core.WebSocketCommandNames.PropertyUpdate,
                Data = JObject.FromObject(dto, PropertyListStepHelper.CamelSerializer),
                Result = WebSockets.Core.WebSocketCommandNames.ResultOk,
            });

            if (!string.IsNullOrWhiteSpace(stepData.Output))
            {
                var items = JArray.Parse(dto.Value ?? "[]");
                variables[stepData.Output] = items.LastOrDefault()?["id"]?.ToString();
            }
        }
    }
}
