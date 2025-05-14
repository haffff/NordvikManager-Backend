using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class CardDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? FirstOpen { get; set; }
        public Guid? TemplateId { get; set; }
        public Guid? Owner { get; set; }
        public Guid? MainResource { get; set; }
        public List<Guid>? AdditionalResources { get; set; }
        public Permission? Permission { get; set; }
        public IEnumerable<PropertyDTO> Properties { get; set; }
    }
}
