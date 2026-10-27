using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Actions.GetActions
{
    public class GetActionByIdCommand : CommandBase<(CommandResponse, ActionDto)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public Guid Id { get; set; }
    }
}
