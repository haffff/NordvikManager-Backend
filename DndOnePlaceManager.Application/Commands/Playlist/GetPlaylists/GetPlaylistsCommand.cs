using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists
{
    public class GetPlaylistsCommand : CommandBase<List<PlaylistDTO>>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;
    }
}
