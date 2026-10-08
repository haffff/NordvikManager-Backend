using Newtonsoft.Json;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Models
{
    public class ActionDefinitionArgument
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        /// <summary>Name of the sibling field that controls visibility.</summary>
        public string? ConditionField { get; set; }
        /// <summary>Required value of <see cref="ConditionField"/> for this argument to be shown.</summary>
        public string? ConditionValue { get; set; }
        /// <summary>True when the argument's value is the name of a variable the step creates.</summary>
        public bool IsOutput { get; set; }
        /// <summary>True when %tokens% in this argument are NOT filled in before the step runs.</summary>
        public bool Deferred { get; set; }
    }

    public class ActionDefinitionResponse
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Value { get; set; }
        /// <summary>Optional one-line template, e.g. "Roll {DiceString}[ → {OutputVariable}]"; [..] segments drop when a placeholder in them is empty.</summary>
        public string? Summary { get; set; }
        public ActionDefinitionArgument[] Arguments { get; set; }
    }

    [JsonObject]
    public class GetActionsDefinitionaResponse
    {
        public ActionDefinitionResponse[] StepDefinitions { get; set; }
    }
}
