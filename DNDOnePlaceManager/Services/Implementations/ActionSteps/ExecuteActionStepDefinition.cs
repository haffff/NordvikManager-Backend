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
        public string Description => "Runs another action, then continues with the next step. The other action gets a copy of the current variables; variables it sets don't come back.";
        public string? Summary => "Run {Value}";
        public Type DataType => typeof(ExecuteActionStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepValue = step.Data.ToObject<ExecuteActionStepData>()?.Value;
            //make it better?
            await gameLobby.ActionProcessingService.ExecActionAsync(stepValue, null, variables);
        }
    }
}
