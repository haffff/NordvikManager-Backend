using DndOnePlaceManager.Application.Exceptions;
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
        public string? Summary => "Read {PropertyName} of {ParentId}[ → {Output}]";
        public Type DataType => typeof(GetPropertyValueStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetPropertyValueStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Output))
                return;

            // No such owner (no id given, a token without a card…): the default, as for a missing property.
            Guid parentGuid;
            try
            {
                parentGuid = await PropertyOwner.ResolveAsync(mediator, gameLobby, variables, "GetPropertyValue", stepData.Of, stepData.ParentId);
            }
            catch (ActionProcessException)
            {
                variables[stepData.Output] = stepData.DefaultValue;
                return;
            }

            var result = await mediator.Send(new GetPropertiesByQueryCommand
            {
                GameId = gameLobby.GameId,
                Player = gameLobby.SystemPlayer,
                ParentIDs = new[] { parentGuid },
                PropertyNames = new[] { stepData.PropertyName },
            });

            variables[stepData.Output] = result?.FirstOrDefault()?.Value ?? stepData.DefaultValue;
        }
    }
}
