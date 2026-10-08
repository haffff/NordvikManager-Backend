using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RunClientCommandStepData
    {
        [Description("ClientMediator panel name to dispatch to (e.g. \"BattleMap_token\").")]
        public string Panel { get; set; }

        [Description("ClientMediator command name to run on that panel (e.g. \"CreateToken\").")]
        public string Command { get; set; }

        [UIType("playerid")]
        [Description("Player to send the command to. Pick one, or type a name / ID / %variable%. Leave empty to broadcast to all — beware duplicated client-side effects (e.g. token creation) if every connected client would run the same command.")]
        public string Player { get; set; }

        [Description("JSON data to pass as the command's payload. Supports %variable% substitution — embed object/array variables unquoted since they're already valid JSON.")]
        public string Data { get; set; }
    }
}
