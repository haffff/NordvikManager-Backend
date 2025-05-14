using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class FilterStepDefinition : IActionStepDefinition
    {
        private static System.Data.DataTable DT = new System.Data.DataTable();

        public string Name => "Filter Collection";
        public string Value => "FilterCollection";
        public string Category => "Collection";

        public string Description => "Filters a collection based on a condition";

        public Type DataType => typeof(FilterStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<FilterStepData>();

            var collectionToFilter = variables[stepData.Collection] as IEnumerable<object>;
            var filteredCollection = collectionToFilter.Where(x =>
            {
                variables[stepData.ItemName] = x;
                var result = DT.Compute(stepData.Condition, "");
                return (bool)result == true;
            }).AsEnumerable();
            variables[stepData.OutputName] = filteredCollection;
        }
    }
}
