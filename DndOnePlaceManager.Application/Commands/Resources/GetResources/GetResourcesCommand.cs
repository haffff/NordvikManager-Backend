using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourcesCommand : CommandBase<(CommandResponse, List<ResourceDTO>)>
    {
        public Guid? GameId { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
