using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    /// <summary>
    /// Music-playlist playback orchestration shared by <c>PlaylistController</c> (HTTP) and
    /// the Play/Pause/Stop Playlist action steps, so the in-memory playback state
    /// (<see cref="GameLobby.ActivePlaylistPlaybacks"/>), resume-in-place behaviour and the
    /// <c>playlist_*</c> broadcasts stay identical on both paths.
    ///
    /// Stateless — the caller supplies the resolved <see cref="GameLobby"/> and a scoped
    /// <see cref="IMediator"/>.
    /// </summary>
    public interface IPlaybackService
    {
        Task<CommandResponse> PlayPlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId);
        Task<CommandResponse> PausePlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId);
        Task<CommandResponse> StopPlaylistAsync(IMediator mediator, GameLobby lobby, PlayerDTO player, Guid playlistId);
    }
}
