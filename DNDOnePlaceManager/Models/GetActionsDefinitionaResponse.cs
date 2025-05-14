using Newtonsoft.Json;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Models
{
    public class ActionDefinitionArgument
    {
        public string Name { get; set; }
        public string Type { get; set; }
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
