using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.Link
{
    // Recursively walks a local directory and links every file it finds (one ResourceModel per
    // file, zero bytes copied), mirroring the on-disk subfolder structure into the existing
    // tree-folder mechanism. GM-only.
    public class LinkDirectoryCommand : CommandBase<(CommandResponse, int LinkedCount)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string LocalDirectoryPath { get; set; }
        public Guid? ParentFolder { get; set; }

        // Optional progress callback, invoked periodically while the walk runs. Never bound
        // from a request body — only ever set when constructing this command directly in code
        // (e.g. the controller's background task) — so a delegate field here is safe.
        public Action<int>? OnProgress { get; set; }
    }
}
