
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.Resources
{
    public class RemoveResourceCommand : CommandBase<CommandResponse>
    {
        public Guid? ID { get; set; }
        public string? Key { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid? GameId { get; set; }
        public bool System { get; set; }
    }
}

