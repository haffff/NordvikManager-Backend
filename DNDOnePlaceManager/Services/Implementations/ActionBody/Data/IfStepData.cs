using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class IfStepData
    {
        [Description("Boolean expression to evaluate. Use %varName% to insert a variable, e.g. '%HP% > 0' or '%q:{playerId}.health% >= 10'. Supports standard math and comparison operators.")]
        public string? Condition { get; set; }

        [Description("Name of the action to execute when the condition is true. Leave empty to do nothing.")]
        public string? ActionTrue { get; set; }

        [Description("Name of the action to execute when the condition is false. Leave empty to do nothing.")]
        public string? ActionFalse { get; set; }

        [Description("Name of the variable where the boolean result will be stored. Leave empty to skip.")]
        public string? OutputName { get; set; }
    }
}
