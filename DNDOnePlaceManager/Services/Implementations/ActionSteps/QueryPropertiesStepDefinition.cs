using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class QueryPropertiesStepDefinition : IActionStepDefinition
    {
        public string Name => "Query Properties";
        public string Value => "QueryProperties";
        public string Description => "Query properties based on names and parentIds or ids";
        public string Category => "Properties";
        public Type DataType => typeof(QueryPropertiesStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<QueryPropertiesStepData>();

            var propNames = stepData.PropertyNames.Split(',');
            var parentIds = stepData.ParentIds.Split(',');
            var ids = stepData.Ids.Split(',');

            GetPropertiesByQueryCommand command = new GetPropertiesByQueryCommand
            {
                PropertyNames = propNames,
                ParentIDs = parentIds.Select(x => Guid.Parse(x)).ToArray(),
                Ids = ids.Select(x => Guid.Parse(x)).ToArray(),
            };

            var result = await mediator.Send(command);

            if (result != null)
            {
                variables[stepData.Output] = result;
            }
        }
    }
}
