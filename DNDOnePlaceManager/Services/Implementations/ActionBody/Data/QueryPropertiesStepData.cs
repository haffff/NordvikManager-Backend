using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class QueryPropertiesStepData
    {
        [Description("Comma-separated GUIDs of the entities whose properties to get (each may be a %variable%). Leave empty to search the whole game.")]
        public string ParentIds { get; set; }

        [Description("Comma-separated property names to get, e.g. 'hp,ac'. Leave empty for all.")]
        public string PropertyNames { get; set; }

        [Description("Comma-separated GUIDs of specific properties (not entities) to get. Leave empty to not filter by property id.")]
        public string Ids { get; set; }

        [VariableOutput]
        [Description("Name of the variable for the result: a list of properties, each with Id, Name, Value and ParentID.")]
        public string Output { get; set; }
    }
}
