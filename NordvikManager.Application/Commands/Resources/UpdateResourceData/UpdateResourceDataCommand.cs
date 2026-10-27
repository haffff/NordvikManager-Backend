using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Resources.UpdateResourceData
{
    public class UpdateResourceDataCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public Guid? Id { get; set; }
        public string? Content { get; set; }
        public string? MimeType { get; set; }
    }
}
