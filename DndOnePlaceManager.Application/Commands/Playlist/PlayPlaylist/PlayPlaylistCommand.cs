using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist
{
    public class PlayPlaylistCommand : CommandBase<PlayPlaylistResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlaylistId { get; set; }
    }

    public class PlayPlaylistResult
    {
        public CommandResponse Response { get; set; }
        public PlaybackMode Mode { get; set; }
        public bool Shuffle { get; set; }
        public bool Repeat { get; set; }
        public List<Guid> TrackOrder { get; set; } = new();
    }
}
