using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ShowMarkerStepData
    {
        [Description("circle, rect or text.")]
        public string? Shape { get; set; }

        [Description("Where, in map pixels: the centre of the shape, e.g. %position.x%.")]
        public string? X { get; set; }

        [Description("Where, in map pixels: the centre of the shape, e.g. %position.y%.")]
        public string? Y { get; set; }

        [Description("Circle: its radius, in squares (e.g. 4 for a 20 ft Fireball on a 5 ft grid).")]
        public string? Radius { get; set; }

        [Description("Rect: its width, in squares.")]
        public string? Width { get; set; }

        [Description("Rect: its height, in squares.")]
        public string? Height { get; set; }

        [Description("Fill colour, e.g. rgba(255,80,0,0.35). Text: its colour.")]
        public string? Color { get; set; }

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
