using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
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
        public List<ResourceDTO> Resources { get; set; } = new();
    }
}
