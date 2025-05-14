using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Properties.UpdateProperties
{
    public class UpdatePropertiesCommand : GamePlayerCommandBase<CommandResponse>
    {
        public PropertyDTO[] Properties { get; set; }
    }
}
