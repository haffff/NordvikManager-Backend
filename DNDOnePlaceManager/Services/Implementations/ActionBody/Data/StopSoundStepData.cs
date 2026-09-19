using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class StopSoundStepData
    {
        [UIType("audioresourceid")]
        [Description("Audio material to stop. Pick one from the list, or type a resource ID / %variable%. " +
                     "Must match the ID used by the Play Sound step / soundboard.")]
        public string ResourceId { get; set; }

        [UIType("playerid")]
        [Description("Player to stop the sound for. Pick one, or type a name / ID / %variable%. " +
                     "Leave empty to stop it for everyone connected to the game.")]
        public string Player { get; set; }
    }
}
