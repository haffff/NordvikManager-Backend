using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>
    /// <see cref="ImportEntityCommand"/> from an export file stored in the game (an addon
    /// can ship a map or a soundboard this way). Only a file uploaded by the system, the GM
    /// or a co-GM (an addon's files are the installing GM's) is imported: an action runs
    /// with the system's rights, and a player's file could carry a card's code.
    /// </summary>
    public class ImportEntityFromResourceCommand : CommandBase<ImportEntityResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }

        /// <summary>The file's key, or its id.</summary>
        public string Resource { get; set; }

        public string? ExpectedKind { get; set; }
        public string? Name { get; set; }
        public string? FolderPath { get; set; }
    }
}
