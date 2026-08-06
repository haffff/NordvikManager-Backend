using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
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
    public class GetPropertyValueStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Property Value";
        public string Value => "GetPropertyValue";
        public string Description => "Reads a single named property's value for one entity, falling back to DefaultValue when it doesn't exist.";
        public string Category => "Properties";
        public Type DataType => typeof(GetPropertyValueStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetPropertyValueStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Output))
                return;

            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
            {
                variables[stepData.Output] = stepData.DefaultValue;
                return;
            }

            var result = await mediator.Send(new GetPropertiesByQueryCommand
            {
                Player = gameLobby.SystemPlayer,
                ParentIDs = new[] { parentGuid },
                PropertyNames = new[] { stepData.PropertyName },
            });

            variables[stepData.Output] = result?.FirstOrDefault()?.Value ?? stepData.DefaultValue;
        }
    }
}
