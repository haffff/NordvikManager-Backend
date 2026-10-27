using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Security.SetViewPermission
{
    /// <summary>
    /// The SetPermission step: share a card, token or map for viewing (Read), or let a player
    /// move a token (Control), or take that back. Nothing else (see the handler).
    /// </summary>
    public class SetViewPermissionCommand : CommandBase<SetViewPermissionResult>
    {
        public Guid GameId { get; set; }

        /// <summary>The player who triggered the action: they must already have Edit and the right they grant.</summary>
        public Guid? CallerId { get; set; }

        public Guid EntityId { get; set; }

        /// <summary>A player of the game, by id or name; empty for everyone.</summary>
        public string? Player { get; set; }

        public Permission Permission { get; set; } = Permission.Read;

        public bool Revoke { get; set; }
    }

    public class SetViewPermissionResult
    {
        /// <summary>card, token or map.</summary>
        public string EntityKind { get; set; } = "";

        public string? EntityName { get; set; }

        /// <summary>The player it was for; null for everyone.</summary>
        public Guid? PlayerId { get; set; }

        /// <summary>Players who couldn't see it before and can now (to tell their clients).</summary>
        public List<Guid> GainedRead { get; set; } = new();

        /// <summary>Players who could see it before and can't now.</summary>
        public List<Guid> LostRead { get; set; } = new();
    }
}
