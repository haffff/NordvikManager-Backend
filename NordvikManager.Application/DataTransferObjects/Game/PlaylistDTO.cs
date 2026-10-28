using NordvikManager.Domain.Enums;
using System;
using System.Collections.Generic;

namespace NordvikManager.Application.DataTransferObjects.Game
{
    public class PlaylistDTO
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PlaybackMode Mode { get; set; }
        public bool Shuffle { get; set; }
        public bool Repeat { get; set; }
        public PlaylistKind Kind { get; set; }
        public double Volume { get; set; } = 1;
        public List<Guid> LoopedResourceIds { get; set; } = new();
        public List<ResourceDTO> Resources { get; set; } = new();
    }
}
