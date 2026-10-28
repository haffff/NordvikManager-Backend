using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Actions.GetActions
{
    public class GetActionsCommand : CommandBase<(CommandResponse, List<ActionDto>)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public int? Page { get; set; }
        public bool flatList { get; set; } = false;
    }
}
