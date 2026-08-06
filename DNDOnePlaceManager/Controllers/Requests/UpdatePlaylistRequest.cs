using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class UpdatePlaylistRequest
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;
        public bool Shuffle { get; set; } = false;
        public bool Repeat { get; set; } = true;
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;
        public List<Guid> ResourceIds { get; set; } = new();
    }
}
