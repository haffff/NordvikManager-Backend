
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class RemoveResourceCommand : CommandBase<CommandResponse>
    {
        public Guid? ID { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid? GameId { get; set; }
        public bool System { get; set; }
    }
}

