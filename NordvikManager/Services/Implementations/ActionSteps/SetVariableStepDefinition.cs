using NordvikManager.Application.Services.Actions;
using NordvikManager.Extensions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NordvikManager.Application.Exceptions;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    public class SetVariableStepDefinition : IActionStepDefinition
    {
        public string Name => "Set Variable";
        public string Value => "SetVariable";
        public IReadOnlyList<string> Keywords => new[] { "remember", "store", "variable", "value" };
        public string Category => "Control Flow";
        public string Description => "Sets a variable to a value, with optional type casting and a default fallback when the value is empty.";
        public string? Summary => "{Name} = {Value}";
        public Type DataType => typeof(SetVariableStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetVariableStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Name))
                throw new ActionProcessException("SetVariable: 'Name' argument is required.");

            var raw = stepData.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(stepData.DefaultValue))
                raw = ActionTokenResolver.ResolveVariables(stepData.DefaultValue, variables);

            var targetType = ActionValueTypes.Resolve(stepData.Type);

            object? varValue;
            try
            {
                varValue = string.IsNullOrWhiteSpace(raw) ? null : Convert.ChangeType(raw, targetType);
            }
            catch
            {
                varValue = raw;
            }

            variables[stepData.Name] = varValue;
            return Task.CompletedTask;
        }
    }
}
