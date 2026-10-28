using NordvikManager.Application.DataTransferObjects;

namespace NordvikManager.Application.Commands.TreeEntry.GetTreeEntries
{
    public class GetTreeEntriesCommand : CommandBase<List<TreeEntryDto>>
    {
        public Guid? PlayerId { get; set; }
        public Guid? GameId { get; set; }
        public string EntityType { get; set; }
    }
}
