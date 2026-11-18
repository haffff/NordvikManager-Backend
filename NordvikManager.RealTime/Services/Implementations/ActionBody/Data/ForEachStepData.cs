using NordvikManager.Models;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class ForEachStepData
    {
        [Description("Name of the variable holding the collection to iterate over.")]
        public string Collection { get; set; }

        [VariableOutput("item", ItemOf = nameof(Collection))]
        [Description("Name used for the current item in each iteration, readable in the steps of Do.")]
        public string ItemName { get; set; }

        [UIType("steps")]
        [Description("Steps to run for each item. They use the action's variables: what they set is there after the loop.")]
        public JArray? Do { get; set; }
    }
}
