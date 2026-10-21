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
    public class IfStepDefinition : IActionStepDefinition, IDeferredArgumentsStep
    {
        [ThreadStatic]
        private static DataTable DT;
        private static DataTable GetDT() => DT ??= new DataTable();

        public string Name => "If";
        public string Value => "If";
        public string Category => "Control Flow";
        public string Description => "Evaluates a true/false condition and runs the Then steps when it is true, the Else steps when it is false. They work on the action's own variables.";
        public string? Summary => "If {Condition}[ → {OutputName}]";
        public Type DataType => typeof(IfStepData);

        // The blocks are filled in step by step as they run, with the variables of that moment.
        private static readonly string[] Blocks = { nameof(IfStepData.Then), nameof(IfStepData.Else) };
        public IReadOnlyCollection<string> DeferredArguments => Blocks;

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var isStep = step.Data.ToObject<IfStepData>();

            var condition = isStep.Condition;

            if (GetDT().Compute(condition, "") is not bool result)
                throw new ActionProcessException($"If: condition '{condition}' did not evaluate to true/false.");

            if (!string.IsNullOrWhiteSpace(isStep.OutputName))
                variables[isStep.OutputName] = result;

            var block = result ? isStep.Then : isStep.Else;
            if (block is { Count: > 0 })
                await gameLobby.ActionProcessingService.RunBlockAsync(block, variables,
                    result ? nameof(IfStepData.Then) : nameof(IfStepData.Else));
        }
    }
}
