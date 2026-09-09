using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DndOnePlaceManager.Application.Exceptions;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class FilterStepDefinition : IActionStepDefinition
    {
        [ThreadStatic]
        private static System.Data.DataTable DT;
        private static System.Data.DataTable GetDT() => DT ??= new System.Data.DataTable();

        public string Name => "Filter Collection";
        public string Value => "FilterCollection";
        public string Category => "Collection";

        public string Description => "Filters a collection based on a condition";

        public Type DataType => typeof(FilterStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
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

            var filteredCollection = collectionToFilter.Where(x =>
            {
                variables[stepData.ItemName] = x;
                var result = GetDT().Compute(stepData.Condition, "");
                return (bool)result == true;
            }).AsEnumerable();
            variables[stepData.OutputName] = filteredCollection;
        }
    }
}
