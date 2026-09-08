using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry
{
    public class RemoveTreeEntryCommandHandler : HandlerBase<RemoveTreeEntryCommand, CommandResponse>
    {
        public RemoveTreeEntryCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(RemoveTreeEntryCommand request, CancellationToken cancellationToken)
        {
            base.Handle(request, cancellationToken);

            var playerId = request.PlayerId ?? Guid.Empty;

            // AsSplitQuery() — see InstallAddonCommandHandler.Handle's own comment
            // for why chaining multiple collection .Include()s without it is a
            // cartesian-explosion risk (confirmed live: a 6-collection version of
            // this pattern took 276s and failed with a disk-full error).
            var game = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Next)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));


            Guard.NotFound(game, "Game", request.GameId);
            Guard.Argument(request.TargetId != null || request.TreeEntryId != null, nameof(request.TargetId), nameof(request.TreeEntryId));

            var treeEntry = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryId || x.TargetId == request.TargetId);

            if (treeEntry == null)
            {
                //No change required
                return CommandResponse.Ok;
            }

            if (treeEntry.IsFolder)
            {
                game.ThrowIfNoPermission(playerId, Permission.Edit);
            }

            if (await dbContext.TreeEntries.AnyAsync(x => x.Parent != null && x.Parent.Id == treeEntry.Id, cancellationToken))
            {
                throw new TreeException("Folder is not empty!");
            }

            var nextFromDeleted = treeEntry?.Next;
            // Re-point EVERY predecessor, not just the first — a corrupted tree can have
            // more than one row with Next == treeEntry.Id (e.g. left over from a botched
            // move), and SQLite's FK check rejects the DELETE if even one is missed.
            var entriesWithNext = game.TreeEntries.Where(x => x.Next?.Id == treeEntry.Id).ToList();
            if (entriesWithNext.Count > 0)
            {
                foreach (var entryWithNext in entriesWithNext)
                {
                    entryWithNext.Next = nextFromDeleted;
                }
            }
            else
            {
                if (nextFromDeleted != null)
                    nextFromDeleted.Head = true;
            }

            // Flush the predecessor fix-up before the delete — SQLite checks FK constraints
            // per-statement and will reject DELETE if another row still has Next = treeEntry.Id.
            await dbContext.SaveChangesAsync(cancellationToken);

            game.TreeEntries.Remove(treeEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            return CommandResponse.Ok;
        }
    }
}
