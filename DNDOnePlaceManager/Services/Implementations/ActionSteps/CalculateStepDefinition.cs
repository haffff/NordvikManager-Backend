using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class CalculateStepDefinition : IActionStepDefinition
    {
        private static System.Data.DataTable DT = new System.Data.DataTable();

        public string Name => "Calculate";
        public string Value => "Calculate";
        public string Category => "Math";
        public string Description => "Performs calculation provided in Expression argument";

        public Type DataType => typeof(CalculateStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<CalculateStepData>();
            var resultValue = DT.Compute(stepData.Expression, "");
            variables[stepData.OutputName] = resultValue;
        }
    }
}
