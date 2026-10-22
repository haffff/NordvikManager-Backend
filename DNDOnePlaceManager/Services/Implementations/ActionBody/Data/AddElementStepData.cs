using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class AddElementStepData
    {
        [Description("circle, rect or text.")]
        public string? Shape { get; set; }

        [Description("Where, in map pixels: the centre of the shape, e.g. %position.x%.")]
        public string? X { get; set; }

        [Description("Where, in map pixels: the centre of the shape, e.g. %position.y%.")]
        public string? Y { get; set; }

        [Description("Circle: its radius, in squares.")]
        public string? Radius { get; set; }

        [Description("Rect: its width, in squares.")]
        public string? Width { get; set; }

        [Description("Rect: its height, in squares.")]
        public string? Height { get; set; }

        [Description("Fill colour, e.g. rgba(80,80,255,0.4). Text: its colour.")]
        public string? Color { get; set; }

        [Description("Text to show (text shape).")]
        public string? Text { get; set; }

        [Description("Layer number (default: the map layer, under tokens).")]
        public string? Layer { get; set; }

        [Description("The map to add it to. Empty: the map the player is looking at.")]
        public string? MapId { get; set; }

        [VariableOutput("value")]
        [Description("Variable for the new element's id (e.g. to remove it later with Delete Element).")]
        public string? Output { get; set; }
    }
}
