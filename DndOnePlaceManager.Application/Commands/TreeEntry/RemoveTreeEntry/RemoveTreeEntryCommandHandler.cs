using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry
{
    public class RemoveTreeEntryCommandHandler : HandlerBase<RemoveTreeEntryCommand,CommandResponse>
    {
        public RemoveTreeEntryCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(RemoveTreeEntryCommand request, CancellationToken cancellationToken)
        {
            base.Handle(request, cancellationToken);

            var playerId = request.PlayerId ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Next)
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));


            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(GameModel));
            }

            if (request.TargetId == null && request.TreeEntryId == null)
            {
                throw new WrongArgumentsException(nameof(request.TargetId), nameof(request.TreeEntryId));
            }

            if (game.TreeEntries.Any(x => request.TreeEntryId != null && x.Parent?.Id == request.TreeEntryId))
            {
                throw new TreeException("Folder is not empty!");
            }

            var treeEntry = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryId || x.TargetId == request.TargetId);

            if (treeEntry == null)
            {
                //No change required
                return CommandResponse.Ok;
            }

            var nextFromDeleted = treeEntry?.Next;
            var entryWithNext = game.TreeEntries.FirstOrDefault(x => (x.Next?.Id == treeEntry.Id));
            if (entryWithNext != null)
            {
                entryWithNext.Next = nextFromDeleted;
            }
            else
            {
                if(nextFromDeleted != null)
                    nextFromDeleted.Head = true;
            }

            game.TreeEntries.Remove(treeEntry);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
