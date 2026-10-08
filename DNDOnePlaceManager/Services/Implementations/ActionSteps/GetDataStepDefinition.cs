using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class GetDataStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Data";
        public string Value => "GetData";
        public string Category => "Data";
        public string Description => "Finds game entities of one type (maps, cards, layouts, actions, map elements or properties) by id, by name, or by having a given property, and stores the list (or the first match) in a variable.";
        public string? Summary => "Get {Type}[ {Name}][ {Id}][ → {Output}]";
        public Type DataType => typeof(GetDataStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetDataStepData>();

            // Stored under this name below; without it .NET reported "Value cannot be null. (Parameter 'key')".
            if (string.IsNullOrWhiteSpace(stepData.Output))
                throw new ActionProcessException("Get Data: 'Output' is required: the name of the variable to store the result in.");

            ActionGetDataCommand command = new ActionGetDataCommand()
            {
                EntityType = stepData.Type,
                Name = stepData.Name,
                Property = stepData.PropertyName,
                GameID = gameLobby.GameId,
                ID = stepData.Id != null ? Guid.Parse(stepData.Id) : null
            };

            var result = await mediator.Send(command);

            if (result != null)
            {
                if (stepData.SingleElement == true)
                {
                    variables[stepData.Output] = result.FirstOrDefault();
                }
                else
                {
                    variables[stepData.Output] = result;

                }
            }
        }
    }
}
