using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Folder.AddFolder
{
    public class AddTreeEntryCommand : CommandBase<(CommandResponse, List<TreeEntryDto>)>
    {
        public PlayerDTO Player { get; set; }
        public Guid? GameId { get; set; }
        public TreeEntryDto TreeEntryDto { get; set; }
    }
}
