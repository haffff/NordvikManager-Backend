using DNDOnePlaceManager.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Builds the step catalogue the action editor renders: names, summaries and, per
    /// argument, the widget type, description, visibility condition, whether it names a
    /// created variable, and whether tokens in it are filled in before the step runs.
    /// </summary>
    public static class StepDefinitionMetadata
    {
        public static ActionDefinitionResponse[] Build(IEnumerable<IActionStepDefinition> definitions) =>
            definitions.Select(Build).ToArray();

        public static ActionDefinitionResponse Build(IActionStepDefinition x)
        {
            var deferred = (x as IDeferredArgumentsStep)?.DeferredArguments ?? Array.Empty<string>();

            return new ActionDefinitionResponse()
            {
                Name = x.Name,
                Value = x.Value,
                Category = x.Category,
                Description = x.Description,
                Summary = x.Summary,
                Arguments = x.DataType?
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(y => y.SetMethod?.IsPublic == true)
                    .Select(y => new ActionDefinitionArgument()
                    {
                        Name = y.Name,
                        Type = y.GetCustomAttribute<UITypeAttribute>()?.Type ?? y.PropertyType.Name,
                        Description = y.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        ConditionField = y.GetCustomAttribute<ShowIfAttribute>()?.Field,
                        ConditionValue = y.GetCustomAttribute<ShowIfAttribute>()?.Value,
                        IsOutput = y.GetCustomAttribute<VariableOutputAttribute>() != null,
                        Deferred = deferred.Contains(y.Name, StringComparer.OrdinalIgnoreCase),
                        Output = Shape(y.GetCustomAttribute<VariableOutputAttribute>()),
                    }).ToArray()
            };
        }

        private static OutputShape? Shape(VariableOutputAttribute? output) => output == null ? null : new OutputShape
        {
            Kind = output.Kind,
            Fields = output.Fields,
            FieldsBy = output.FieldsBy,
            FieldsByValue = output.FieldsFrom?.GetField("Fields", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                is IReadOnlyDictionary<string, string[]> byValue
                    ? byValue.ToDictionary(x => x.Key, x => x.Value)
                    : null,
            SingleWhen = output.SingleWhen,
            ValueWhen = output.ValueWhen,
            ItemOf = output.ItemOf,
        };
    }
}
