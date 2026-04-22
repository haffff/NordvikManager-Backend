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
    public class SetResourceStepDefinition : IActionStepDefinition
    {
        public string Name        => "Set Resource";
        public string Value       => "SetResource";
        public string Category    => "Data";
        public string Description => "Creates or updates a text resource identified by a unique key. If the key already exists the content is overwritten; otherwise a new resource is created.";
        public Type   DataType    => typeof(SetResourceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetResourceStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Key))
                throw new ActionProcessException("SetResource: 'Key' is required.");

            var id = await mediator.Send(new SetResourceCommand
            {
                GameId = gameLobby.GameId,
                Player = new PlayerDTO { Id = gameLobby.SystemPlayer.Id, Name = gameLobby.SystemPlayer.Name },
                Key    = stepData.Key,
                Name   = stepData.Name ?? stepData.Key,
                Data   = Encoding.UTF8.GetBytes(stepData.Content ?? string.Empty),
            });

            if (!string.IsNullOrWhiteSpace(stepData.OutputVariable))
                variables[stepData.OutputVariable] = id.ToString();
        }
    }
}
