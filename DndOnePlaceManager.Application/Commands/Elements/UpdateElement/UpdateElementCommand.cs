
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Elements
{
    public class UpdateElementCommand : CommandBase<CommandResponse>
    {
        public ElementDTO Element { get; set; }
        public PlayerDTO Player { get; set; }
    }
}

