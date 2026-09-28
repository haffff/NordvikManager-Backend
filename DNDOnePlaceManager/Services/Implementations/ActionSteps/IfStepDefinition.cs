using DndOnePlaceManager.Application.Exceptions;
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
        [ThreadStatic]
        private static DataTable DT;
        private static DataTable GetDT() => DT ??= new DataTable();

        public string Name => "If";
        public string Value => "If";
        public string Category => "Control Flow";
        public string Description => "Executes action based on condition";
        public string? Summary => "If {Condition}[ → {OutputName}]";
        public Type DataType => typeof(IfStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var isStep = step.Data.ToObject<IfStepData>();

            var condition = isStep.Condition;

            if (GetDT().Compute(condition, "") is not bool result)
                throw new ActionProcessException($"If: condition '{condition}' did not evaluate to true/false.");

            // Written before branching so the branch action (which gets a copy of variables) sees it too.
            if (!string.IsNullOrWhiteSpace(isStep.OutputName))
                variables[isStep.OutputName] = result;

            string action = result ? isStep.ActionTrue : isStep.ActionFalse;
            if (string.IsNullOrWhiteSpace(action))
                return;

            await gameLobby.ActionProcessingService.ExecActionAsync(action, null, variables);
        }
    }
}
