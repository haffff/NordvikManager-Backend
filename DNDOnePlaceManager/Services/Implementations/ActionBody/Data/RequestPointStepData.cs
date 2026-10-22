using DNDOnePlaceManager.Models;
using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RequestPointStepData
    {
        [UIType("playerid")]
        [Description("Who points. Empty: the player who ran the action.")]
        public string? Player { get; set; }

        [Description("What the player is told, e.g. 'Where does the Fireball go off?'.")]
        public string? Message { get; set; }

        [DefaultValue("circle")]
        [Description("The area that follows the cursor: circle (default), rect, line, cone, text or none (just a point). " +
            "A line or cone starts at From and the player only aims it.")]
        public string? Shape { get; set; }

        [ShowIf("Shape", "circle|line|cone")]
        [Description("Circle: its radius, in squares (e.g. 4 for a 20 ft Fireball on a 5 ft grid); line, cone: its length.")]
        public string? Radius { get; set; }

        [ShowIf("Shape", "rect|line")]
        [Description("Rect: its width, in squares; line: its width (default 1).")]
        public string? Width { get; set; }

        [ShowIf("Shape", "rect")]
        [Description("Rect: its height, in squares.")]
        public string? Height { get; set; }

        [ShowIf("Shape", "cone")]
        [Description("Cone: how wide it spreads, in degrees (default 53).")]
        public string? Angle { get; set; }

        [Description("Fill colour, e.g. rgba(255,80,0,0.35); text: its colour. Empty: the colour of the player who points.")]
        public string? Color { get; set; }

        [ShowIf("Shape", "text")]
        [Description("Text to show (text shape).")]
        public string? Text { get; set; }

        [Description("none (default), center (the middle of a square) or corner (where grid lines cross).")]
        public string? SnapToGrid { get; set; }

        [Description("Line, cone: where it starts (the caster's token: its id, or a token from Find / Request Targets; or a point, e.g. %position%). " +
            "Circle, rect: where MaxDistance counts from; from a token it counts from its edge, like Find's Near.")]
        public string? From { get; set; }

        [ShowIf("Shape", "circle|rect|text|none")]
        [Description("Circle, rect: how far from From the point may be, in squares (e.g. 30 for 150 ft). Empty: anywhere.")]
        public string? MaxDistance { get; set; }

        [ShowIf("Shape", "circle|rect|line|cone")]
        [DefaultValue(true)]
        [Description("Tokens the marker covers glow while the player aims (the ones Find with Near and Radius would give).")]
        public bool HighlightTokens { get; set; } = true;

        [DefaultValue(true)]
        [Description("Everyone looking at the map sees the marker and the range move. Off: only the player who points.")]
        public bool ShowToOthers { get; set; } = true;

        [Description("The map to point on. Empty: the map the player is looking at.")]
        public string? MapId { get; set; }

        [Description("How long to wait for the player (default 2 minutes).")]
        public TimeSpan? Timeout { get; set; }

        [DefaultValue(true)]
        [Description("Stop the action when the player cancels, doesn't answer or isn't connected. Off: carry on with an empty Output.")]
        public bool StopIfCancelled { get; set; } = true;

        [VariableOutput("object", "x", "y", "mapId", "from")]
        [Description("Variable for the point, in map pixels: { x, y, mapId, from }. A line or cone: x, y is the end of its axis and from its start (else from is empty).")]
        public string? Output { get; set; }
    }
}
