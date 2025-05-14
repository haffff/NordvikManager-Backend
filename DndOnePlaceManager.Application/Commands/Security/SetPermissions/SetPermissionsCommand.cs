using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Security.SetPermissions
{
    public class SetPermissionsCommand : CommandBase<CommandResponse>
    {
        public string EntityType { get; set; }
        public Guid EntityId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid GameID { get; set; }
        public Dictionary<Guid,Permission?> Permissions { get; set; }
    }
}
