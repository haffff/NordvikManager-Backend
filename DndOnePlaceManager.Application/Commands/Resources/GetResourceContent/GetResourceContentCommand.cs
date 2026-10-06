using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.GetResourceContent
{
    /// <summary>
    /// A resource's bytes (or its thumbnail) with the version a client can cache them
    /// under. Null if the resource or its file doesn't exist.
    /// </summary>
    public class GetResourceContentCommand : CommandBase<ResourceContent?>
    {
        public Guid? ID { get; set; }
        public string? Key { get; set; }
        public Guid? GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public bool Thumbnail { get; set; }
        public int MaxDimension { get; set; } = 256;

        /// <summary>The version the client has cached; if still current, no bytes are returned.</summary>
        public string? IfVersion { get; set; }
    }

    /// <param name="Data">Null when <paramref name="NotModified"/>.</param>
    public record ResourceContent(byte[]? Data, MimeType MimeType, string Version, bool NotModified);
}
