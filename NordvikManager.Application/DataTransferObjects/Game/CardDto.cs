using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.DataTransferObjects.Game
{
    public class CardDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? Key { get; set; }
        public bool? FirstOpen { get; set; }
        public Guid? TemplateId { get; set; }
        // Nullable so an update that leaves it out doesn't unhide the card.
        public bool? IsHidden { get; set; }
        public Guid? Owner { get; set; }
        public Guid? MainResource { get; set; }
        public List<Guid>? AdditionalResources { get; set; }
        public Permission? Permission { get; set; }
        public Permission? GenericPermission { get; set; }
        public Permission? GmPermission { get; set; }
        public IEnumerable<PropertyDTO> Properties { get; set; }

        /// <summary>The addon whose code the card runs (its page is that addon's); null for none (filled by GetCard).</summary>
        public string? AddonKey { get; set; }

        /// <summary>Where that addon may connect (its info.json "network"); null when it doesn't say (filled by GetCard).</summary>
        public AddonNetworkDto? AddonNetwork { get; set; }
    }
}
