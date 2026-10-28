using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Security.SetViewPermission
{
    /// <summary>
    /// What an action may change about permissions, and no more:
    /// - only Read (see it) or Control (move a token), never Edit (a card's code runs with the
    ///   rights of whoever opens it), Remove or Execute (an action written for GMs would
    ///   become a player tool);
    /// - only card instances, tokens/elements and maps of this game; not templates, views,
    ///   actions, layouts, properties, resources or the game;
    /// - only by the player who triggered the action, who must have Edit on it and the right
    ///   they grant; never the rows of the game's owner or system player, never lowering
    ///   someone who can edit it; "everyone" only gets Read.
    /// So it does nothing the caller couldn't do in the Permissions dialog.
    /// </summary>
    internal class SetViewPermissionCommandHandler : HandlerBase<SetViewPermissionCommand, SetViewPermissionResult>
    {
        private readonly IPermissionService permissions;

        public SetViewPermissionCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissions) : base(dbContext, mapper)
        {
            this.permissions = permissions;
        }

        public override async Task<SetViewPermissionResult> Handle(SetViewPermissionCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var level = request.Permission;
            if (level != Permission.Read && level != Permission.Control)
                throw new ActionProcessException("SetPermission: only Read (see it) or Control (move a token) can be given or taken back.");

            var game = await dbContext.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken)
                ?? throw new ActionProcessException("SetPermission: the game wasn't found.");

            var (entity, kind, name) = await FindAsync(request.EntityId, game.Id, cancellationToken)
                ?? throw new ActionProcessException($"SetPermission: '{request.EntityId}' isn't a card, token or map of this game (templates, views, actions and the game itself can't be changed this way).");

            var caller = request.CallerId ?? Guid.Empty;
            if (caller == Guid.Empty)
                throw new ActionProcessException("SetPermission: needs the player who triggered the action; it can't run from a hook without one.");
            if (!permissions.CheckIfHasPermissions(caller, entity, Permission.Edit))
                throw new PermissionException(Permission.Edit);
            if (!permissions.CheckIfHasPermissions(caller, entity, level))
                throw new PermissionException(level);

            Guid? target = null;
            if (!string.IsNullOrWhiteSpace(request.Player))
            {
                var wanted = request.Player.Trim();
                var player = game.Players.FirstOrDefault(p => p.Id.ToString() == wanted)
                    ?? game.Players.FirstOrDefault(p => string.Equals(p.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ActionProcessException($"SetPermission: '{wanted}' isn't a player of this game.");
                if (player.Id == game.MasterId || player.Id == game.SystemPlayerId)
                    throw new ActionProcessException("SetPermission: the game owner's and the system's rights can't be changed.");
                target = player.Id;
            }
            else if (level != Permission.Read)
            {
                throw new ActionProcessException("SetPermission: everyone can only be given Read; give Control to one player.");
            }

            var players = game.Players.Where(p => p.Id != game.MasterId && p.Id != game.SystemPlayerId).Select(p => p.Id).ToList();
            var readersBefore = players.Where(p => permissions.CheckIfHasPermissions(p, entity, Permission.Read)).ToHashSet();

            if (target is Guid one)
            {
                var current = permissions.GetPermission(one, entity) ?? Permission.None;
                if (request.Revoke)
                {
                    if ((current & (Permission.Edit | Permission.Remove)) != 0)
                        throw new ActionProcessException("SetPermission: that player can edit it; change their rights in the Permissions dialog.");
                    permissions.UnsetPermission(one, entity, level);
                }
                else if ((current & level) != level)
                {
                    permissions.SetPermission(one, entity, level);
                }
            }
            else
            {
                var everyone = permissions.GetPermission(Guid.Empty, entity) ?? Permission.None;
                permissions.SetGenericPermissions(entity, request.Revoke ? everyone & ~Permission.Read : everyone | Permission.Read);
            }

            var readersAfter = players.Where(p => permissions.CheckIfHasPermissions(p, entity, Permission.Read)).ToHashSet();
            return new SetViewPermissionResult
            {
                EntityKind = kind,
                EntityName = name,
                PlayerId = target,
                GainedRead = readersAfter.Except(readersBefore).ToList(),
                LostRead = readersBefore.Except(readersAfter).ToList(),
            };
        }

        private async Task<(IEntity Entity, string Kind, string? Name)?> FindAsync(Guid id, Guid gameId, CancellationToken cancellationToken)
        {
            var card = await dbContext.Cards.FirstOrDefaultAsync(c => c.Id == id && c.GameId == gameId, cancellationToken);
            if (card != null)
                return card.IsTemplate || card.IsCustomUi ? null : (card, "card", card.Name);

            var element = await dbContext.Elements.FirstOrDefaultAsync(e => e.Id == id && e.Map!.Game.Id == gameId, cancellationToken);
            if (element != null)
                return (element, "token", null);

            var map = await dbContext.Maps.FirstOrDefaultAsync(m => m.Id == id && m.Game.Id == gameId, cancellationToken);
            if (map != null)
                return (map, "map", map.Name);

            return null;
        }
    }
}
