using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class FindStepData
    {
        [UIType("findkind")]
        [Description("What to find: tokens, elements, cards, maps, battleMaps, players, actions or layouts.")]
        public string? What { get; set; }

        [Description("Only the one with this id.")]
        public string? Id { get; set; }

        [Description("Only those with this name.")]
        public string? Name { get; set; }

        [Description("Only those that have a property with this name (cards, maps, tokens, elements), e.g. 'hostile'.")]
        public string? Property { get; set; }

        [Description("Tokens and elements: the map to look on. Empty: the map the player is looking at.")]
        public string? MapId { get; set; }

        [Description("Tokens and elements: look on every map of the game.")]
        public bool AllMaps { get; set; }

        [Description("Tokens: only those linked to a card.")]
        public bool WithCard { get; set; }

        [Description("Tokens and elements: only those in an area at this point ({x, y}, e.g. %point%) or token (its id, or %target%): " +
            "its centre (circle, rect) or start (line, cone)…")]
        public string? Near { get; set; }

        [DefaultValue("circle")]
        [Description("…of this shape: circle (default), rect, line or cone. A token counts when it touches the area.")]
        public string? Shape { get; set; }

        [ShowIf("Shape", "circle|line|cone")]
        [Description("Circle: its radius, in squares; line, cone: its length.")]
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

        [ShowIf("Shape", "line|cone")]
        [Description("Line, cone: the point or token it points at, e.g. %point%.")]
        public string? Toward { get; set; }

        [Description("Cards: include templates too.")]
        public bool IncludeTemplates { get; set; }

        [Description("Give only the first one found (or nothing), instead of a list.")]
        public bool First { get; set; }

        [VariableOutput("list", FieldsBy = nameof(What), FieldsFrom = typeof(DndOnePlaceManager.Application.Commands.Actions.Find.FindShapes), SingleWhen = nameof(First))]
        [Description("Variable to store the result in.")]
        public string? Output { get; set; }
    }
}
