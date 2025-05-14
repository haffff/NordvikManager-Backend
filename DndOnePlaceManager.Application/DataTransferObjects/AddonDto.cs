using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.DataTransferObjects
{
    public class AddonDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? License { get; set; }
        public List<AddonDto>? Dependencies { get; set; }
        public List<CardDto>? Views { get; set; }
        public List<CardDto>? Templates { get; set; }
        public List<ActionDto>? Actions { get; set; }
        public List<ResourceDTO>? Resources { get; set; }
        public Permission? Permission { get; set; }
        public DateTime? Installed { get; set; }
    }
}
