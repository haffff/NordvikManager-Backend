using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class ExecuteActionStepDefinition : IActionStepDefinition
    {
        public string Name => "Execute Action";
        public string Value => "ExecuteAction";
        public string Category => "Control Flow";
        public string Description => "Executes action provided as argument";
        public Type DataType => typeof(ValueStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepValue = step.Data.ToObject<ValueStepData>()?.Value;
            //make it better?
            await gameLobby.ActionProcessingService.ExecActionAsync(stepValue, null, variables);
        }
    }
}
