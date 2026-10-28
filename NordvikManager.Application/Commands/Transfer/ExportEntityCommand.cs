using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>
    /// A map, card or playlist as an <see cref="EntityExportDto"/>, everything on it
    /// included (a map's hidden elements too): only who may edit the game exports. Null if
    /// the game has no such entity.
    /// </summary>
    public class ExportEntityCommand : CommandBase<EntityExportDto?>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }

        /// <summary>One of <see cref="EntityTransferKinds"/>; any card kind finds any card, the export says which it is.</summary>
        public string Kind { get; set; }
        public Guid Id { get; set; }

        /// <summary>Put the files it uses into the export too (base64), for another game that lacks them.</summary>
        public bool IncludeFiles { get; set; }
    }
}
