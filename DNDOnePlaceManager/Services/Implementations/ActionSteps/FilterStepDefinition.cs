using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class FilterStepDefinition : IActionStepDefinition, IDeferredArgumentsStep
    {
        [ThreadStatic]
        private static System.Data.DataTable DT;
        private static System.Data.DataTable GetDT() => DT ??= new System.Data.DataTable();

        public string Name => "Filter Collection";
        public string Value => "FilterCollection";
        public string Category => "Collection";
        public string Description => "Filters a collection based on a condition. The condition is evaluated once per item, " +
            "with the current item available as %ItemName% (or %v:ItemName.field% for objects). " +
            "Property queries (%q:...% / %qn:...%) are not resolved inside Condition — read them into a variable first.";
        public Type DataType => typeof(FilterStepData);

        // Condition references the per-item variable, so it must not be substituted up front.
        public IReadOnlyCollection<string> DeferredArguments { get; } = new[] { nameof(FilterStepData.Condition) };

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<FilterStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Collection))
                throw new ActionProcessException("FilterCollection: 'Collection' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.ItemName))
                throw new ActionProcessException("FilterCollection: 'ItemName' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.Condition))
                throw new ActionProcessException("FilterCollection: 'Condition' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.OutputName))
                throw new ActionProcessException("FilterCollection: 'OutputName' argument is required.");

            if (!variables.ContainsKey(stepData.Collection))
                throw new ActionProcessException($"FilterCollection: variable '{stepData.Collection}' not found.");

            var collectionToFilter = variables[stepData.Collection] as IEnumerable<object>
                ?? throw new ActionProcessException($"FilterCollection: variable '{stepData.Collection}' is not a collection.");

            var hadItem = variables.TryGetValue(stepData.ItemName, out var previousItem);
            var filtered = new List<object>();
            try
            {
                foreach (var item in collectionToFilter.ToList())
                {
                    variables[stepData.ItemName] = item;
                    var condition = VariablePathResolver.ResolveTokens(stepData.Condition, variables).Prepare(variables);
                    if (GetDT().Compute(condition, "") is not bool keep)
                        throw new ActionProcessException($"FilterCollection: condition '{condition}' did not evaluate to true/false.");
                    if (keep)
                        filtered.Add(item);
                }
            }
            finally
            {
                if (hadItem)
                    variables[stepData.ItemName] = previousItem;
                else
                    variables.Remove(stepData.ItemName);
            }

            variables[stepData.OutputName] = filtered;
            return Task.CompletedTask;
        }
    }
}
