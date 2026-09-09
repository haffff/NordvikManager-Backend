using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class DeleteResourceStepDefinition : IActionStepDefinition
    {
        public string Name        => "Delete Resource";
        public string Value       => "DeleteResource";
        public string Category    => "Data";
        public string Description => "Permanently deletes a resource identified by key or ID. Also removes any associated tree entries.";
        public Type   DataType    => typeof(DeleteResourceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<DeleteResourceStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Key) && string.IsNullOrWhiteSpace(stepData.ResourceId))
                throw new ActionProcessException("DeleteResource: 'Key' or 'ResourceId' is required.");

            Guid.TryParse(stepData.ResourceId, out var resourceGuid);

            var player = new PlayerDTO { Id = gameLobby.SystemPlayer.Id, Name = gameLobby.SystemPlayer.Name };

            await mediator.Send(new RemoveResourceCommand
            {
                GameId = gameLobby.GameId,
                Player = player,
                System = true,
                Key    = stepData.Key,
                ID     = resourceGuid == Guid.Empty ? null : resourceGuid,
            });
        }
    }
}
