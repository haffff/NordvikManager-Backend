
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourceDataCommand : CommandBase<(byte[], MimeType)>
    {
        public Guid? ID { get; set; }
        public string? Key { get; set; }
        public Guid? GameID { get; set; }
        public PlayerDTO Player { get; set; }
    }
}

