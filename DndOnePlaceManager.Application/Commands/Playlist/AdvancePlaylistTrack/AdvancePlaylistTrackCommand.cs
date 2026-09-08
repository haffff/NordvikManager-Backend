using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack
{
    public class AdvancePlaylistTrackCommand : CommandBase<AdvancePlaylistTrackResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlaylistId { get; set; }
        public List<Guid> CurrentTrackOrder { get; set; } = new();
        public int CurrentTrackIndex { get; set; }
        public bool Shuffle { get; set; }
        public bool Repeat { get; set; }
    }

    public class AdvancePlaylistTrackResult
    {
        public CommandResponse Response { get; set; }
        public bool Ended { get; set; }
        public List<Guid> NextTrackOrder { get; set; } = new();
        public int NextTrackIndex { get; set; }
        public Guid? NextTrackId { get; set; }
    }
}
