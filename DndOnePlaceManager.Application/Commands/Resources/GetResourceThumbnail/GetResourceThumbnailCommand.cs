using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourceThumbnailCommand : CommandBase<(byte[], MimeType)>
    {
        public Guid? ID { get; set; }
        public string? Key { get; set; }
        public Guid? GameID { get; set; }
        public int MaxDimension { get; set; } = 256;
        public PlayerDTO Player { get; set; }
    }
}
