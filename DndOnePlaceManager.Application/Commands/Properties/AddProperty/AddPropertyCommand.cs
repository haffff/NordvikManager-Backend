using System;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class AddPropertyCommand : CommandBase<(CommandResponse, Guid)>
    {
        public PlayerDTO Player { get; set; }
        public PropertyDTO Property { get; set; }
    }
}
