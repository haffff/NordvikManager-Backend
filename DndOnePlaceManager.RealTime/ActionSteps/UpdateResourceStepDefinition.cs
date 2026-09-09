using DndOnePlaceManager.Application.Commands.Resources.SetResource;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class UpdateResourceStepDefinition : IActionStepDefinition
    {
        public string Name        => "Update Resource";
        public string Value       => "UpdateResource";
        public string Category    => "Data";
        public string Description => "Overwrites the content of an existing text resource identified by key or ID. Creates the resource if it does not exist.";
        public Type   DataType    => typeof(UpdateResourceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<UpdateResourceStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Key) && string.IsNullOrWhiteSpace(stepData.ResourceId))
                throw new ActionProcessException("UpdateResource: 'Key' or 'ResourceId' is required.");

            // Resolve the key: prefer explicit Key, fall back to ResourceId as key for SetResourceCommand.
            var key = !string.IsNullOrWhiteSpace(stepData.Key)
                ? stepData.Key
                : stepData.ResourceId;

            await mediator.Send(new SetResourceCommand
            {
                GameId   = gameLobby.GameId,
                Player   = new PlayerDTO { Id = gameLobby.SystemPlayer.Id, Name = gameLobby.SystemPlayer.Name },
                Key      = key,
                Name     = key,
                Data     = Encoding.UTF8.GetBytes(stepData.Content ?? string.Empty),
                MimeType = stepData.MimeType,
            });
        }
    }
}
