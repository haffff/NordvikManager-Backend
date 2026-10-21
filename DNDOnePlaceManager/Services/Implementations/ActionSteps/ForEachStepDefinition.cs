using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class ForEachStepDefinition : IActionStepDefinition, IDeferredArgumentsStep
    {
        public string Name => "For Each";
        public string Value => "ForEach";
        public IReadOnlyList<string> Keywords => new[] { "loop", "each", "every", "all", "repeat", "list" };
        public string Category => "Control Flow";
        public string Description => "Runs the Do steps once for each item of a list variable (e.g. the tokens Find found). In them the current item is the variable named in Item Name; they work on the action's own variables.";
        public string? Summary => "For each {ItemName} in {Collection}";
        public Type DataType => typeof(ForEachStepData);

        // Filled in on every pass, with that item.
        private static readonly string[] Blocks = { nameof(ForEachStepData.Do) };
        public IReadOnlyCollection<string> DeferredArguments => Blocks;

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ForEachStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Collection))
                throw new ActionProcessException("ForEach: 'Collection' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.ItemName))
                throw new ActionProcessException("ForEach: 'ItemName' argument is required.");
            if (!variables.ContainsKey(stepData.Collection))
                throw new ActionProcessException($"ForEach: variable '{stepData.Collection}' not found.");

            var collection = variables[stepData.Collection] as IEnumerable<object>
                ?? throw new ActionProcessException($"ForEach: variable '{stepData.Collection}' is not a collection.");

            foreach (var item in collection)
            {
                variables[stepData.ItemName] = item;
                await gameLobby.ActionProcessingService.RunBlockAsync(stepData.Do, variables, nameof(ForEachStepData.Do));
            }
        }
    }
}
