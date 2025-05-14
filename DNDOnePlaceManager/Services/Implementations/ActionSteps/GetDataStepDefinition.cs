using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
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
        public string Description => "Gets data from database and sets variable with name provided as output argument";
        public Type DataType => typeof(GetDataStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetDataStepData>();

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
                if(stepData.SingleElement == true)
                {
                    variables[stepData.Output] = result.FirstOrDefault();
                }
                else
                {
                    variables[stepData.Output] = result;
                }
            }
        }

        public string GetName()
        {
            return "GetData";
        }
    }
}
