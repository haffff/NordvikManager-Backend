using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class ForEachStepDefinition : IActionStepDefinition
    {
        public string Name => "For Each";
        public string Value => "ForEach";
        public string Category => "Control Flow";
        public string Description => "Executes action for each item in collection";
        public Type DataType => typeof(ForEachStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ForEachStepData>();
            var collection = variables[stepData.Collection] as IEnumerable<object>;
            foreach (var item in collection)
            {
                variables[stepData.ItemName] = item;
                await gameLobby.ActionProcessingService.ExecActionAsync(stepData.Action, null, variables);
            }
        }
    }
}
