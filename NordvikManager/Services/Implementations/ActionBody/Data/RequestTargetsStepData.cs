using NordvikManager.Models;
using System;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class RequestTargetsStepData
    {
        [UIType("playerid")]
        [Description("Who picks. Empty: the player who ran the action.")]
        public string? Player { get; set; }

        [Description("What can be picked: token (default) or element (any element on the map: a shape, a picture, a token).")]
        public string? Target { get; set; }

        [Description("What the player is told, e.g. 'Pick up to 3 creatures to bless'.")]
        public string? Message { get; set; }

        [Description("Fewest to pick (default 1).")]
        public string? Min { get; set; }

        [Description("Most to pick (default 1).")]
        public string? Max { get; set; }

        [Description("Only these can be picked: a list of tokens or ids, e.g. %inRange% from a Find. Empty: any token the player can see.")]
        public string? Candidates { get; set; }

        [Description("How long to wait for the player (default 2 minutes).")]
        public TimeSpan? Timeout { get; set; }

        [DefaultValue(true)]
        [Description("Stop the action when the player cancels, doesn't answer or isn't connected. Off: carry on with an empty list.")]
        public bool StopIfCancelled { get; set; } = true;

        [VariableOutput("list", "id", "name", "x", "y", "w", "h", "cardId", "mapId")]
        [Description("Variable for the picked tokens (or elements), in the order they were picked.")]
        public string? Output { get; set; }
    }
}
