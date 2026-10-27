using NordvikManager.Application.DataTransferObjects;

namespace NordvikManager.Application.Exceptions
{
    public class TreeException : Exception
    {
        public TreeException(string message, TreeEntryDto? dto = null) : base(message)
        {
            this.Dto = dto;
        }

        public TreeEntryDto Dto { get; private set; }
    }
}
