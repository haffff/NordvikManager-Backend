using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.DataTransferObjects.Game
{
    public class BattleMapDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public Guid? MapId { get; set; }
        public Guid? GameId { get; set; }
        public Permission? Permission { get; set; }
    }
}
