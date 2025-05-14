using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SetVariableStepDefinition : IActionStepDefinition
    {
        public string Name => "Set Variable";
        public string Value => "SetVariable";
        public string Category => "Control Flow";
        public string Description => "Sets variable";
        public Type DataType => typeof(SetVariableStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetVariableStepData>();
            Type type = Type.GetType(stepData.Type) ?? typeof(System.String);
            var varValue = step.Data["Value"].ToObject(type);
            variables[stepData.Name] = varValue;
        }
    }
}
