using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class RemovePropertyCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid Id { get; set; }
    }
}
