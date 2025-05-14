using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class IfStepDefinition : IActionStepDefinition
    {
        private static DataTable DT = new System.Data.DataTable();

        public string Name => "If";
        public string Value => "If";
        public string Category => "Control Flow";
        public string Description => "Executes action based on condition";
        public Type DataType => typeof(IfStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var isStep = step.Data.ToObject<IfStepData>();

            var condition = isStep.Condition;

            var result = DT.Compute(condition, "");

            //await DebugLog(mediator, new { Step = step, Message = "Comparsion finished with:", Value = condition }, false);
            string action = (bool)result ? isStep.ActionTrue : isStep.ActionFalse;

            await gameLobby.ActionProcessingService.ExecActionAsync(action, null, variables);
        }
    }
}
