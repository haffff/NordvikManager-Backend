using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData
{
    public class DeleteResourceDataCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public Guid? Id { get; set; }
    }
}
