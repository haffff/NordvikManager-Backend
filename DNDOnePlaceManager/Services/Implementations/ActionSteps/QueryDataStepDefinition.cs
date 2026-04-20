using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class QueryDataStepDefinition : IActionStepDefinition
    {
        public string Name => "Query Data";
        public string Value => "QueryData";
        public string Category => "Data";
        public string Description => "Batch variable assignment. Each line in Assignments is 'variableName=value'. " +
                                     "Supports %q:% and %qn:% query syntax which is resolved before the step runs.";
        public Type DataType => typeof(QueryDataStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<QueryDataStepData>();

            if (string.IsNullOrWhiteSpace(stepData?.Assignments))
                return Task.CompletedTask;

            foreach (var line in stepData.Assignments.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx <= 0)
                    continue;

                var varName = trimmed[..eqIdx].Trim();
                var value   = trimmed[(eqIdx + 1)..].Trim();

                if (!string.IsNullOrEmpty(varName))
                    variables[varName] = value;
            }

            return Task.CompletedTask;
        }
    }
}
