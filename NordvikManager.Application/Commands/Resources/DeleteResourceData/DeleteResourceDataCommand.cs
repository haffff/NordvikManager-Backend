using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Resources.DeleteResourceData
{
    public class DeleteResourceDataCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public Guid? Id { get; set; }
    }
}
