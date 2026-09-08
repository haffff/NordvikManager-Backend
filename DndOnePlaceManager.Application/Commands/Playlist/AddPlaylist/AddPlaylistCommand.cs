using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist
{
    public class AddPlaylistCommand : CommandBase<(CommandResponse, Guid)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;
        public bool Shuffle { get; set; } = false;
        public bool Repeat { get; set; } = true;
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;
        public List<Guid> ResourceIds { get; set; } = new();
    }
}
