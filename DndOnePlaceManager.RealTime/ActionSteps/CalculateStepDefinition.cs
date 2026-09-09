using DndOnePlaceManager.Application.Exceptions;
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
        [ThreadStatic]
        private static System.Data.DataTable DT;
        private static System.Data.DataTable GetDT() => DT ??= new System.Data.DataTable();

        public string Name => "Calculate";
        public string Value => "Calculate";
        public string Category => "Math";
        public string Description => "Performs calculation provided in Expression argument";

        public Type DataType => typeof(CalculateStepData); public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<CalculateStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Expression))
                throw new ActionProcessException("Calculate: 'Expression' argument is required.");

            if (string.IsNullOrWhiteSpace(stepData.OutputName))
                throw new ActionProcessException("Calculate: 'OutputName' argument is required.");

            var resultValue = GetDT().Compute(stepData.Expression, "");
            variables[stepData.OutputName] = resultValue;
        }
    }
}
