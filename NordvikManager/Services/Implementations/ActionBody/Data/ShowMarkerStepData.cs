using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class ShowMarkerStepData
    {
        [UIType("markershape")]
        [DefaultValue("circle")]
        [Description("circle (default), rect, line, cone or text.")]
        public string? Shape { get; set; }

        [Description("Where: a point ({x, y}, e.g. %point%) or a token (its id, or %target%). The centre of a circle, rect or text; " +
            "where a line or cone starts.")]
        public string? At { get; set; }

        [ShowIf("Shape", "line|cone")]
        [Description("Line, cone: the point or token it points at.")]
        public string? Toward { get; set; }

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

        [Description("Fill colour, e.g. rgba(255,80,0,0.35); text: its colour. Empty: the colour of the player whose action it is.")]
        public string? Color { get; set; }

        [ShowIf("Shape", "text")]
        [Description("Text to show (text shape).")]
        public string? Text { get; set; }

        [Description("How long it stays, in seconds (default 10).")]
        public string? Duration { get; set; }

        [Description("Stays until the action ends instead (e.g. while the GM decides), then goes away.")]
        public bool UntilActionEnds { get; set; }

        [Description("The map to show it on. Empty: the map the player is looking at.")]
        public string? MapId { get; set; }

        [VariableOutput("value")]
        [Description("Variable for the marker's id.")]
        public string? Output { get; set; }
    }
}
