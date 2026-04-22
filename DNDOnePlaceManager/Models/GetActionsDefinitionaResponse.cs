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
    }

    public class ActionDefinitionResponse
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Value { get; set; }
        public ActionDefinitionArgument[] Arguments { get; set; }

    }

    [JsonObject]
    public class GetActionsDefinitionaResponse
    {
        public ActionDefinitionResponse[] StepDefinitions { get; set; }
    }
}
