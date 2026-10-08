using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetDetailStepData
    {
        [Description("Name of the variable (without %) holding the object to change. Only the variable changes: nothing is saved to the game.")]
        public string? Input { get; set; }

        [Description("Field to set. For a map element: a canvas key (see Is Element). Otherwise the object's field name, case-sensitive, e.g. 'Name'.")]
        public string? DetailName { get; set; }

        [Description("Value to set, converted to Type. Supports %variable% substitution.")]
        public string? Value { get; set; }

        [Description("Type to convert Value to: 'string' (default), 'int', 'long', 'float', 'double' or 'bool'.")]
        public string? Type { get; set; }

        [Description("Turn on when Input holds a map element (token, shape): DetailName is then a key of its canvas data, e.g. 'left', 'fill'.")]
        public bool isElement { get; set; }
    }
}
