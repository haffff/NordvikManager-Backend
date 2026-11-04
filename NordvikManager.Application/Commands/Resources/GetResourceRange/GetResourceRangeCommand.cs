using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Resources.GetResourceRange
{
    /// <summary>
    /// Part of a resource's bytes (Start, up to Length), with its total size and the version
    /// a client can cache the part under. Null if the resource or its file doesn't exist.
    /// </summary>
    public class GetResourceRangeCommand : CommandBase<ResourceRange?>
    {
        public Guid? ID { get; set; }
        public string? Key { get; set; }
        public Guid? GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public long Start { get; set; }
        public int Length { get; set; }

        /// <summary>The version the client has this part cached under; if still current, no bytes are returned.</summary>
        public string? IfVersion { get; set; }
    }

    /// <param name="Data">Null when <paramref name="NotModified"/>; empty past the end.</param>
    public record ResourceRange(byte[]? Data, long Start, long Total, MimeType MimeType, string Version, bool NotModified);
}
