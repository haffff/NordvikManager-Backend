using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class IfStepData
    {
        [Description("Boolean expression to evaluate, e.g. '{HP} > 0'.")]
        public string? Condition { get; set; }

        [Description("Label of the step to jump to when the condition is true.")]
        public string? TrueLabel { get; set; }

        [Description("Label of the step to jump to when the condition is false.")]
        public string? FalseLabel { get; set; }

        [Description("Name of the variable where the boolean result will be stored.")]
        public string? OutputName { get; set; }

        public string ActionTrue { get; internal set; }
        public string ActionFalse { get; internal set; }
    }
}
