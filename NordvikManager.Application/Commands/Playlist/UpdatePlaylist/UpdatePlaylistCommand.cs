using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Playlist.UpdatePlaylist
{
    public class UpdatePlaylistCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlaylistId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;
        public bool Shuffle { get; set; } = false;
        public bool Repeat { get; set; } = true;
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;
        public List<Guid> ResourceIds { get; set; } = new();

        /// <summary>0..1; unchanged when not given.</summary>
        public double? Volume { get; set; }
    }
}
