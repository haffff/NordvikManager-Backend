using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using System;

namespace DndOnePlaceManager.Application.Commands.Actions
{
    public class UpdateActionCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public ActionDto Action { get; set; }
    }
}
