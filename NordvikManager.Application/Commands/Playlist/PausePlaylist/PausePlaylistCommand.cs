using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Playlist.PausePlaylist
{
    public class PausePlaylistCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlaylistId { get; set; }
    }
}
