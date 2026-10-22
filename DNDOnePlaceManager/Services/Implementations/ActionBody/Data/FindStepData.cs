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

        [Description("Tokens and elements: only those near this point ({x, y}, e.g. %position%) or token (its id)…")]
        public string? Near { get; set; }

        [Description("…within this many squares (counted to the token's edge).")]
        public string? Radius { get; set; }

        [Description("Cards: include templates too.")]
        public bool IncludeTemplates { get; set; }

        [Description("Give only the first one found (or nothing), instead of a list.")]
        public bool First { get; set; }

        [VariableOutput("list", FieldsBy = nameof(What), FieldsFrom = typeof(DndOnePlaceManager.Application.Commands.Actions.Find.FindShapes), SingleWhen = nameof(First))]
        [Description("Variable to store the result in.")]
        public string? Output { get; set; }
    }
}
