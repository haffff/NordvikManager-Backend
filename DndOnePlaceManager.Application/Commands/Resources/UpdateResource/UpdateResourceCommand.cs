using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.Resources.UpdateResource
{
    public class UpdateResourceCommand : CommandBase<CommandResponse>
    {
        public Guid? gameID { get; set; }
        public PlayerDTO? Player { get; set; }
        public User? User { get; set; }
        public ResourceDTO Resource { get; set; }
    }
}
