using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData
{
    public class UpdateResourceDataCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public Guid? Id { get; set; }
        public string? Content { get; set; }
        public string? MimeType { get; set; }
    }
}
