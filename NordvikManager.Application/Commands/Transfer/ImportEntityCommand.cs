using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>
    /// Makes a new map, card or playlist from an <see cref="EntityExportDto"/>, with new
    /// ids, in any game the player may edit. Files are found by id, then by key, then
    /// taken from the export; what's still missing is left out (a token or picture falls
    /// back to the empty one) and named in the result.
    /// </summary>
    public class ImportEntityCommand : CommandBase<ImportEntityResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public EntityExportDto Export { get; set; }

        /// <summary>Refuse anything but this kind (the panel it's imported into); null takes any.</summary>
        public string? ExpectedKind { get; set; }

        /// <summary>A name instead of the exported one (a duplicate's "… (copy)").</summary>
        public string? Name { get; set; }

        /// <summary>The folder (tree entry) to put it in; null for the top level.</summary>
        public Guid? TreeParentId { get; set; }

        /// <summary>A folder path ("Maps/Dungeons"), made where missing, used when there's no TreeParentId.</summary>
        public string? FolderPath { get; set; }

        /// <summary>Put it right after this tree entry (the original, for a duplicate).</summary>
        public Guid? AfterTreeEntryId { get; set; }

        /// <summary>Put it right after the tree entry of this map, card or playlist (when there's no AfterTreeEntryId).</summary>
        public Guid? AfterTargetId { get; set; }
    }

    /// <param name="Id">The new entity.</param>
    /// <param name="TreeEntry">Its folder-tree entry.</param>
    /// <param name="Missing">The names of the files it uses that this game doesn't have.</param>
    /// <param name="NewFolders">Folders made for <see cref="ImportEntityCommand.FolderPath"/>.</param>
    public record ImportEntityResult(Guid Id, string Kind, TreeEntryDto? TreeEntry, List<string> Missing, List<TreeEntryDto> NewFolders);
}
