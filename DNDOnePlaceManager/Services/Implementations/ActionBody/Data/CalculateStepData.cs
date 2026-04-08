using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class CalculateStepData
    {
        [Description("Math expression to evaluate, e.g. '2 + 3' or '{HP} * 2'. Supports variable references.")]
        public string Expression { get; set; }

        [Description("Name of the variable where the calculation result will be stored.")]
        public string OutputName { get; set; }
    }
}
