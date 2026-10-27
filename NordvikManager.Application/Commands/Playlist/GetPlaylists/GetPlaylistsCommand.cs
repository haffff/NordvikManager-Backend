using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Playlist.GetPlaylists
{
    public class GetPlaylistsCommand : CommandBase<List<PlaylistDTO>>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;
        /// <summary>The caller runs this server, so may see where its files are.</summary>
        public bool IsLocalAdmin { get; set; }
    }
}
