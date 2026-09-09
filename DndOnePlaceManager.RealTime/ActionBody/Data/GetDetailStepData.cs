using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDetailStepData
    {
        [Description("Name of the variable holding the entity to read a detail from.")]
        public string? Input { get; set; }

        [Description("If true, treats the input as a collection element rather than a top-level entity.")]
        public bool IsElement { get; set; }

        [Description("Name of the detail/property to read from the entity.")]
        public string? DetailName { get; set; }

        [Description("Name of the variable where the detail value will be stored.")]
        public string? Output { get; set; }
    }
}
