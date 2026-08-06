using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Services.Implementations
{
    // Ephemeral, per-lobby playback state — never persisted, lives only on the owning
    // GameLobby instance for as long as the playlist is actively playing or paused.
    public class PlaylistPlaybackState
    {
        public Guid PlaylistId { get; set; }
        public PlaybackMode Mode { get; set; }
        public bool Shuffle { get; set; }
        public bool Repeat { get; set; }
        public List<Guid> TrackOrder { get; set; } = new();
        public int CurrentTrackIndex { get; set; }
        public bool IsPaused { get; set; }
        public DateTime CurrentTrackStartedAtUtc { get; set; }
    }
}
