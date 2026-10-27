using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Actions
{
    public class AddActionCommand : CommandBase<(CommandResponse, Guid)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public ActionDto Action { get; set; }
    }
}

