using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.UpdateEntry
{
    public class UpdateTreeEntryCommand : CommandBase<(CommandResponse, List<TreeEntryDto>)>
    {
        public TreeEntryDto TreeEntryDto { get; set; }
        public Guid? PlayerId { get; set; }
        public Guid? GameId { get; set; }
    }
}
