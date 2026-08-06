using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Resources.Link
{
    // Read-only local filesystem listing, GM-only. Path omitted lists drive roots.
    public class BrowseLocalDirectoryCommand : CommandBase<IReadOnlyList<LocalDirectoryEntry>>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Path { get; set; }
    }
}
