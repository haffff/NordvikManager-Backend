using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry
{
    public class RemoveTreeEntryCommand : CommandBase<CommandResponse>
    {
        public Guid? TargetId { get; set; }
        public Guid? TreeEntryId { get; set; }
        public Guid? PlayerId { get; set; }
        public Guid? GameId { get; set; }
    }
}
