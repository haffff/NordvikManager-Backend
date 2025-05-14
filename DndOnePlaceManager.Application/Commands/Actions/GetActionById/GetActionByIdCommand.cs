using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Actions.GetActions
{
    public class GetActionByIdCommand : CommandBase<(CommandResponse, ActionDto)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public Guid Id { get; set; }
    }
}
