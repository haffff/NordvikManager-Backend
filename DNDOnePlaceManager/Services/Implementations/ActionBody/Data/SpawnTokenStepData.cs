using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SpawnTokenStepData
    {
        [Description("The card to place a token of (it needs a \"token\" property naming its token file), e.g. %copy%.")]
        public string? CardId { get; set; }

        [Description("Without a card: the token file (resource key), e.g. basics_token_generic.json.")]
        public string? Token { get; set; }

        [Description("Without a card: the picture (resource key or id).")]
        public string? Image { get; set; }

        [Description("Where, in map pixels: the token's top-left corner, e.g. %position.x%.")]
        public string? X { get; set; }

        [Description("Where, in map pixels: the token's top-left corner, e.g. %position.y%.")]
        public string? Y { get; set; }

        [Description("The map. Empty: the map the player is looking at.")]
        public string? MapId { get; set; }

        [VariableOutput("value")]
        [Description("Variable for the new token's id.")]
        public string? Output { get; set; }
    }
}
