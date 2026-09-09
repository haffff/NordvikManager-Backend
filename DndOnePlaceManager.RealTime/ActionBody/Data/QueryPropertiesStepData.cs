using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class QueryPropertiesStepData
    {
        [Description("Comma-separated list of parent entity IDs (or variable names) to query properties from.")]
        public string ParentIds { get; set; }

        [Description("Comma-separated list of property names to retrieve.")]
        public string PropertyNames { get; set; }

        [Description("Comma-separated list of specific entity IDs to filter by. Leave empty to query all under the parent.")]
        public string Ids { get; set; }

        [Description("Name of the variable where the queried properties will be stored.")]
        public string Output { get; set; }
    }
}
