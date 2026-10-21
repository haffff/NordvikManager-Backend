using DNDOnePlaceManager.Models;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class IfStepData
    {
        [Description("Boolean expression to evaluate. Use %varName% to insert a variable, e.g. '%HP% > 0' or '%q:{playerId}.health% >= 10'. Supports standard math and comparison operators.")]
        public string? Condition { get; set; }

        [UIType("steps")]
        [Description("Steps to run when the condition is true. They use the action's variables: what they set is there after the If.")]
        public JArray? Then { get; set; }

        [UIType("steps")]
        [Description("Steps to run when the condition is false.")]
        public JArray? Else { get; set; }

        [VariableOutput]
        [Description("Name of the variable where the boolean result will be stored. Leave empty to skip.")]
        public string? OutputName { get; set; }
    }
}
