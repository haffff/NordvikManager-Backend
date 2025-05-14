using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Actions
{
    public class AddActionCommand : CommandBase<(CommandResponse, Guid)>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public ActionDto Action { get; set; }
    }
}

