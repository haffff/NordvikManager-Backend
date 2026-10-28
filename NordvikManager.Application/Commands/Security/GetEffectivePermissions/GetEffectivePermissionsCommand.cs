using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Security.GetEffectivePermissions
{
    /// <summary>
    /// Who in the game may do what with an entity, for deciding who receives its live
    /// updates: each row's player, plus every co-GM (Edit on the game) with at least the
    /// owner's bits. Players with no row are left out (the "everyone" row is under Guid.Empty).
    /// </summary>
    public class GetEffectivePermissionsCommand : CommandBase<Dictionary<Guid, Permission>>
    {
        public Guid GameId { get; set; }
        public Guid EntityId { get; set; }
    }
}
