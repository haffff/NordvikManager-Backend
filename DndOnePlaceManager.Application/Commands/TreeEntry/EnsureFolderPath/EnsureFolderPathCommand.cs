using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Generic.Command;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.EnsureFolderPath
{
    /// <summary>
    /// The folder at <see cref="Path"/> ("Lore/Trilium") in one tree, made level by level
    /// where missing. Answers its entry id (null for an empty path: the top level) and the
    /// folders it made.
    /// </summary>
    public class EnsureFolderPathCommand : GamePlayerCommandBase<(Guid? FolderId, List<TreeEntryDto> Created)>
    {
        /// <summary>The tree, one of <see cref="Services.TreeEntryTypes"/>.</summary>
        public string EntryType { get; set; }

        /// <summary>Folder names separated by "/"; blanks around them and empty names are ignored.</summary>
        public string? Path { get; set; }
    }
}
