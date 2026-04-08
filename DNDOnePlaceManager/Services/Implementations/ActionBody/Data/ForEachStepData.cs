using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ForEachStepData
    {
        [Description("Name of the variable holding the collection to iterate over.")]
        public string Collection { get; set; }

        [Description("Name used for the current item in each iteration. Accessible as a variable inside the sub-action.")]
        public string ItemName { get; set; }

        [Description("Name of the action to execute for each item in the collection.")]
        public string Action { get; set; }
    }
}
