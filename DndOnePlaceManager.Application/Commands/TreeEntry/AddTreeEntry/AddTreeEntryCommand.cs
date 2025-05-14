using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Folder.AddFolder
{
    public class AddTreeEntryCommand : CommandBase<(CommandResponse, List<TreeEntryDto>)>
    {
        public PlayerDTO Player { get; set; }
        public Guid? GameId { get; set; }
        public TreeEntryDto TreeEntryDto { get; set; }
    }
}
