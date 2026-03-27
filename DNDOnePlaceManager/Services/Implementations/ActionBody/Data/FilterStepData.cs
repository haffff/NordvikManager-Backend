using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class FilterStepData
    {
        [Description("Name of the variable holding the collection to filter.")]
        public string? Collection { get; set; }

        [Description("Name used for the current item within the filter condition.")]
        public string? ItemName { get; set; }

        [Description("Boolean expression used to filter items, e.g. '{Item.HP} > 0'.")]
        public string? Condition { get; set; }

        [Description("Name of the variable where the filtered collection will be stored.")]
        public string? OutputName { get; set; }
    }
}
