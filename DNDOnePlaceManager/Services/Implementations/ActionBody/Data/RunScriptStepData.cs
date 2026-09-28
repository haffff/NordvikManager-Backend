using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RunScriptStepData
    {
        [Description("JavaScript function body. Action variables are available read-only as `vars` (e.g. vars.hp, vars.output.StatusCode). " +
            "Use `return` to hand values back, e.g. `return { hp: Math.max(0, vars.hp - vars.damage) };`. %var% tokens are NOT substituted here — use vars instead.")]
        public string? Script { get; set; }

        [Description("Optional. When set, the whole return value is stored in this variable. " +
            "When empty, the script must return an object and each of its keys becomes a variable.")]
        public string? Output { get; set; }
    }
}
