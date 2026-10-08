using DNDOnePlaceManager.Models;
using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDataStepData
    {
        [UIType("entitytype")]
        [Description("What to get: Map, Card, Layout, Action, Element or Property.")]
        public string? Type { get; set; }

        [Description("Find entities with exactly this name. Used only when Id and PropertyName are empty.")]
        public string? Name { get; set; }

        [Description("Find the entities of this Type that have a property with this name (e.g. 'hp'). Used when Id is empty; Name is then ignored.")]
        public string? PropertyName { get; set; }

        [VariableOutput]
        [Description("Name of the variable for the result: a list of matching entities, or one entity when Single Element is on.")]
        public string? Output { get; set; }

        [Description("GUID of the entity to get (or a %variable% holding one). Takes priority over PropertyName and Name.")]
        public string? Id { get; set; }

        [Description("If true, stores only the first match (or nothing) instead of a list.")]
        public bool SingleElement { get; set; }
    }
}
