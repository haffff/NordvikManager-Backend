using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.TreeEntry.UpdateEntry
{
    public class UpdateTreeEntryCommand : CommandBase<(CommandResponse, List<TreeEntryDto>)>
    {
        public TreeEntryDto TreeEntryDto { get; set; }
        public Guid? PlayerId { get; set; }
        public Guid? GameId { get; set; }
    }
}
