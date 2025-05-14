using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Models
{
    public class CreateAddonInputModel
    {
        public Guid GameId { get; set;}
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Website { get; set; }
        public string? License { get; set; }

        public List<Guid> Views { get; set; }
        public List<Guid> Actions { get; set; }
        public List<Guid> Resources { get; set; }
    }
}
