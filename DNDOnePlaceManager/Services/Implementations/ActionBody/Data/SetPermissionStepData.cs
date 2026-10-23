using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetPermissionStepData
    {
        [Description("The card, token or map, by id (e.g. %cardId%, %q:card[The Old Mill].id%). Templates, views, actions and the game can't be changed this way.")]
        public string? EntityId { get; set; }

        [UIType("playerid")]
        [Description("Player to give it to (or take it from). Pick one, or type a name / ID / %variable%. Leave empty for everyone (Read only).")]
        public string? Player { get; set; }

        [Description("Read (see it: a card in the Cards panel, a map) or Control (move a token). Nothing else can be given by an action. Defaults to Read.")]
        public string? Permission { get; set; }

        [Description("Take it back instead. Never lowers a player who can edit it, the game's owner or the system.")]
        public bool Revoke { get; set; }
    }
}
