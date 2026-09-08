using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    // Shared by the Play / Pause / Stop Playlist steps — each only needs the playlist id.
    public class PlaylistStepData
    {
        [UIType("playlistid")]
        [Description("Music playlist. Pick one from the list, or type a playlist ID / %variable%.")]
        public string PlaylistId { get; set; }
    }
}
