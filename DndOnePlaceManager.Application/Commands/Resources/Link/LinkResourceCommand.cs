using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.Link
{
    // Registers a reference to a file already sitting somewhere on the server's local disk —
    // zero bytes copied. Local admin only, since it exposes the local filesystem of whoever
    // runs this app's backend.
    public class LinkResourceCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Name { get; set; }
        public string LocalPath { get; set; }
        /// <summary>MIME type description string (e.g. "image/png"). Inferred from the file
        /// extension when omitted.</summary>
        public string? MimeType { get; set; }
        public Guid? ParentFolder { get; set; }
        /// <summary>The caller runs this server; only they may link its files.</summary>
        public bool IsLocalAdmin { get; set; }
    }
}
