using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class PlaySoundStepData
    {
        [UIType("audioresourceid")]
        [Description("Audio material to play. Pick one from the list, or type a resource ID / %variable%. " +
                     "You can copy an ID from the Materials panel (the \"Copy ID\" button).")]
        public string ResourceId { get; set; }

        [UIType("playerid")]
        [Description("Player to play the sound for. Pick one, or type a name / ID / %variable%. " +
                     "Leave empty to play it for everyone connected to the game.")]
        public string Player { get; set; }
    }
}
