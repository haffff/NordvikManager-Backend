using DndOnePlaceManager.Application.Commands.Resources;
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
    public class ReadResourceStepDefinition : IActionStepDefinition
    {
        public string Name        => "Read Resource";
        public string Value       => "ReadResource";
        public string Category    => "Data";
        public string Description => "Reads a text resource by key or ID and stores its content in a variable.";
        public Type   DataType    => typeof(ReadResourceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ReadResourceStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Key) && string.IsNullOrWhiteSpace(stepData.ResourceId))
                throw new ActionProcessException("ReadResource: 'Key' or 'ResourceId' is required.");
            if (string.IsNullOrWhiteSpace(stepData.OutputVariable))
                throw new ActionProcessException("ReadResource: 'OutputVariable' is required.");

            Guid.TryParse(stepData.ResourceId, out var resourceGuid);

            var (data, _) = await mediator.Send(new GetResourceDataCommand
            {
                GameID = gameLobby.GameId,
                Player = new PlayerDTO { Id = gameLobby.SystemPlayer.Id, Name = gameLobby.SystemPlayer.Name },
                Key    = stepData.Key,
                ID     = resourceGuid == Guid.Empty ? null : resourceGuid,
            });

            if (data == null)
                throw new ActionProcessException($"ReadResource: no resource found with key='{stepData.Key}' id='{stepData.ResourceId}'.");

            variables[stepData.OutputVariable] = Encoding.UTF8.GetString(data);
        }
    }
}
