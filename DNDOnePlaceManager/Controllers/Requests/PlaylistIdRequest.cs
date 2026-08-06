using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class PlaylistIdRequest
    {
        public Guid PlaylistId { get; set; }

        // Only used by AdvanceTrack: the track index the caller believes it's advancing
        // from. Lets the controller no-op a stale/duplicate advance call (e.g. two GM
        // tabs open) instead of double-skipping a track. Null skips the check.
        public int? FromTrackIndex { get; set; }
    }
}
