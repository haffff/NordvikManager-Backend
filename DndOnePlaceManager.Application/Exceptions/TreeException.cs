using DndOnePlaceManager.Application.DataTransferObjects;

namespace DndOnePlaceManager.Application.Exceptions
{
    public class TreeException : Exception
    {
        public TreeException(string message, TreeEntryDto? dto = null) : base(message) {
            this.Dto = dto;
        }

        public TreeEntryDto Dto { get; private set; }
    }
}
