using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.CreateResource
{
    /// <summary>
    /// Creates a new resource. Returns AlreadyExists if the key is already taken within the game.
    /// </summary>
    public class CreateResourceCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        /// <summary>MIME type description string (e.g. "text/plain"). Defaults to None when absent.</summary>
        public string? MimeType { get; set; }
        /// <summary>Tree-entry parent folder ID. Placed at root when null.</summary>
        public Guid? ParentFolder { get; set; }
    }
}
