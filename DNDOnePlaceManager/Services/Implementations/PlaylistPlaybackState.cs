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

        /// <summary>The playlist's volume (the GM's), 0..1; changes live with playlist_volume.</summary>
        public double Volume { get; set; } = 1;

        /// <summary>Tracks with a volume of their own (file volume), by id.</summary>
        public Dictionary<Guid, double> TrackVolumes { get; set; } = new();

        /// <summary>What clients get in playlist_play and on resync (GetCurrentPlayback).</summary>
        public object ToMessage() => new
        {
            playlistId = PlaylistId,
            mode = Mode,
            shuffle = Shuffle,
            repeat = Repeat,
            trackOrder = TrackOrder,
            currentTrackIndex = CurrentTrackIndex,
            currentTrackStartedAtUtc = CurrentTrackStartedAtUtc,
            isPaused = IsPaused,
            volume = Volume,
            trackVolumes = TrackVolumes,
        };
    }
}
