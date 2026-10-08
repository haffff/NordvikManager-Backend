using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDetailStepData
    {
        [Description("Name of the variable (without %) holding the object to read from, e.g. the output of Get Data.")]
        public string? Input { get; set; }

        [Description("Turn on when Input holds a map element (token, shape): DetailName is then a key of its canvas data, e.g. 'left', 'top', 'fill'.")]
        public bool IsElement { get; set; }

        [Description("Field to read. For a map element: a canvas key (see Is Element). Otherwise the object's field name, case-sensitive, e.g. 'Name' or 'Id'.")]
        public string? DetailName { get; set; }

        [VariableOutput]
        [Description("Name of the variable where the detail value will be stored.")]
        public string? Output { get; set; }
    }
}
