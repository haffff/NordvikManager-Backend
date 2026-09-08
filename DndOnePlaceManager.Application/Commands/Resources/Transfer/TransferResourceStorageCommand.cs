using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.Transfer
{
    // Moves an existing resource's bytes between storage modes. TargetStorage must be Blob or
    // ManagedFile — you cannot transfer *to* Linked, since that inherently means "point at a
    // GM-chosen external file," not "convert an existing resource." Transferring a Linked
    // resource copies its bytes in and leaves the GM's original external file untouched
    // ("adopting" it); transferring a ManagedFile resource away deletes the old on-disk copy
    // once the new location is written.
    public class TransferResourceStorageCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid ResourceId { get; set; }
        public ResourceStorageKind TargetStorage { get; set; }
    }
}
