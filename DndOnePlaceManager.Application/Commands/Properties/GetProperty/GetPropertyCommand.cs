using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProperty
{
    public class GetPropertyCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid? Id { get; set; }
        public Guid? ParentID { get; set; }
        public string Name { get; set; }
    }
}
