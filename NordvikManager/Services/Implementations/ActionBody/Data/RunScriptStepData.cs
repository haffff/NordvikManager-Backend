using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class RunScriptStepData
    {
        [Description("JavaScript function body. Action variables are available read-only as `vars` (e.g. vars.hp, vars.output.StatusCode). " +
            "Use `return` to hand values back, e.g. `return { hp: Math.max(0, vars.hp - vars.damage) };`. %var% tokens are NOT substituted here — use vars instead.")]
        [UIType("code")]
        public string? Script { get; set; }

        [Description("Optional. Shared script files to put before the script (resource keys, one per line or comma-separated), " +
            "e.g. myaddon_rules.js: functions several actions use.")]
        public string? Include { get; set; }

        [Description("Optional. When set, the whole return value is stored in this variable. " +
            "When empty, the script must return an object and each of its keys becomes a variable.")]
        [VariableOutput("script")]
        public string? Output { get; set; }
    }
}
