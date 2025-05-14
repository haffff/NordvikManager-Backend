using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class UpdatePropertyCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public PropertyDTO Property { get; set; }
    }
}
