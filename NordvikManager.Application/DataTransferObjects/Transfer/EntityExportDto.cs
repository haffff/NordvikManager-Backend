using NordvikManager.Domain.Enums;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.DataTransferObjects.Transfer
{
    /// <summary>
    /// A map, card or playlist as a file: Export JSON writes one, Import JSON (and
    /// Duplicate, which is the two in a row) reads it back with new ids. Serialized with
    /// Newtonsoft in camelCase, as the WebSocket messages are.
    /// </summary>
    public class EntityExportDto
    {
        public const string FormatName = "nordvik-entity";
        public const int CurrentVersion = 1;

        public string Format { get; set; } = FormatName;
        public int Version { get; set; } = CurrentVersion;

        /// <summary>One of <see cref="Commands.Transfer.EntityTransferKinds"/>.</summary>
        public string Kind { get; set; }
        public string? Name { get; set; }

        /// <summary>The entity as its own DTO: MapDTO (with its elements), CardDto or PlaylistDTO.</summary>
        public JObject Data { get; set; }

        /// <summary>Every file the entity refers to, so another game can find it by key or take the copy.</summary>
        public List<ExportedResourceDto> Resources { get; set; } = new();

        /// <summary>Who may do what with the entity (and a map's elements), by their ids in the export.</summary>
        public List<ExportedPermissionDto> Permissions { get; set; } = new();

        /// <summary>A map's: the cards of its tokens, each an export of its own; null when not included.</summary>
        public List<EntityExportDto>? Cards { get; set; }
    }

    public class ExportedResourceDto
    {
        public Guid Id { get; set; }
        public string? Key { get; set; }
        public string Name { get; set; }
        /// <summary>The MIME type text ("image/png").</summary>
        public string? MimeType { get; set; }
        /// <summary>The file itself, base64; only when the export was asked to include files.</summary>
        public string? Data { get; set; }
    }

    public class ExportedPermissionDto
    {
        public Guid EntityId { get; set; }
        /// <summary>Empty for the row that applies to everyone (<see cref="All"/>).</summary>
        public Guid PlayerId { get; set; }
        public bool All { get; set; }
        public Permission Permission { get; set; }
    }
}
