using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Map.UpdateMap
{
    public class UpdateMapCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public MapDTO Map { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
