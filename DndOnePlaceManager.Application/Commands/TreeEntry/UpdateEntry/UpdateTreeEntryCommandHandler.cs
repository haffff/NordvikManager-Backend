using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.UpdateEntry
{
    internal class UpdateTreeEntryCommandHandler : HandlerBase<UpdateTreeEntryCommand, (CommandResponse, List<TreeEntryDto>)>
    {
        public UpdateTreeEntryCommandHandler(IDbContext unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }

        public override async Task<(CommandResponse, List<TreeEntryDto>)> Handle(UpdateTreeEntryCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.PlayerId ?? Guid.Empty;

            // AsSplitQuery(): Players + TreeEntries are separate collection navigations
            // on one query — EF Core's default SingleQuery joins them into one
            // cartesian-product result set. See InstallAddonCommandHandler.Handle's
            // own comment for the full story (a 6-collection version of this same
            // pattern took 276s and failed with a disk-full error on a loaded game).
            var game = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.TreeEntries.Where(x => x.NewItem != true)).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries.Where(x => x.NewItem != true)).ThenInclude(x => x.Next)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));

            Guard.NotFound(game, "Game", request.GameId);

            var treeEntry = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.Id);
            Guard.NotFound(treeEntry, "TreeEntry", request.TreeEntryDto.Id);

            if (treeEntry.IsFolder)
            {
                game.ThrowIfNoPermission(playerId, Permission.Edit);
            }

            treeEntry.Name = request.TreeEntryDto.Name ?? treeEntry.Name;
            treeEntry.Color = request.TreeEntryDto.Color ?? treeEntry.Color;
            treeEntry.Icon = request.TreeEntryDto.Icon ?? treeEntry.Icon;

            List<TreeEntryDto> affectedTreeEntries = new List<TreeEntryDto>();

            if (request.TreeEntryDto.Next == treeEntry.Next?.Id && treeEntry.Parent?.Id == request.TreeEntryDto.ParentId)
            {
                dbContext.SaveChanges();
                return (CommandResponse.Ok, new List<TreeEntryDto>() { mapper.Map<TreeEntryDto>(treeEntry) });
            }

            Guard.Argument(
                request.TreeEntryDto.ParentId != request.TreeEntryDto.Id && request.TreeEntryDto.Next != request.TreeEntryDto.Id,
                nameof(request.TreeEntryDto.ParentId), nameof(request.TreeEntryDto.Next));

            DisconnectOldReferences(request, game, treeEntry, affectedTreeEntries);

            treeEntry.Parent = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.ParentId);

            var result = ConnectNewReferences(request, game, treeEntry, affectedTreeEntries);

            return result;
        }

        private (CommandResponse, List<TreeEntryDto>) ConnectNewReferences(UpdateTreeEntryCommand request, GameModel game, TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries)
        {
            var parentId = request.TreeEntryDto.ParentId;
            var next = request.TreeEntryDto.Next;
            var nextModel = game.TreeEntries.FirstOrDefault(x => x.Id == next);

            if (next == null)
            {
                if (game.TreeEntries.Any(x => x.Parent?.Id == parentId && x != treeEntry))
                {
                    return CreateLastItem(game, treeEntry, affectedTreeEntries, parentId);
                }

                return CreateFirstItem(game, treeEntry, affectedTreeEntries, parentId);
            }

            Guard.Argument(nextModel != null, nameof(request.TreeEntryDto.Next));

            if (parentId == null && next != null)
            {
                parentId = nextModel.Parent?.Id;
            }

            //Find current current next id of entry to update (old next id)
            var oldNext = game.TreeEntries.FirstOrDefault(x => x.Next?.Id == nextModel.Id);

            //No oldNext, this is new head
            if (oldNext == null && nextModel.Head == true)
            {
                return ConnectAsFirstItem(treeEntry, affectedTreeEntries, nextModel);
            }
            else
            {
                return ConnectAsItem(treeEntry, affectedTreeEntries, nextModel, oldNext);
            }
        }

        private (CommandResponse, List<TreeEntryDto>) CreateLastItem(GameModel game, TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries, Guid? parentId)
        {
            var lastItem = game.TreeEntries.FirstOrDefault(x => x.Parent?.Id == parentId && x != treeEntry && x.Next == null);
            if (lastItem == null)
                return CreateFirstItem(game, treeEntry, affectedTreeEntries, parentId);
            lastItem.Next = treeEntry;

            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(treeEntry));
            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(lastItem));

            dbContext.SaveChanges();
            return (CommandResponse.Ok, affectedTreeEntries);
        }

        private (CommandResponse, List<TreeEntryDto>) ConnectAsItem(TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries, TreeEntryModel? nextModel, TreeEntryModel? oldNext)
        {

            oldNext.Next = treeEntry;
            treeEntry.Next = nextModel;

            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(treeEntry));
            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(oldNext));

            dbContext.SaveChanges();
            return (CommandResponse.Ok, affectedTreeEntries);
        }

        private (CommandResponse, List<TreeEntryDto>) ConnectAsFirstItem(TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries, TreeEntryModel? nextModel)
        {
            treeEntry.Head = true;
            nextModel.Head = false;
            treeEntry.Next = nextModel;

            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(treeEntry));
            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(nextModel));

            dbContext.SaveChanges();
            return (CommandResponse.Ok, affectedTreeEntries);
        }

        private (CommandResponse, List<TreeEntryDto>) CreateFirstItem(GameModel game, TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries, Guid? parentId)
        {
            treeEntry.Parent = game.TreeEntries.FirstOrDefault(x => x.Id == parentId);
            treeEntry.Head = true;

            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(treeEntry));

            dbContext.SaveChanges();
            return (CommandResponse.Ok, affectedTreeEntries);
        }

        private void DisconnectOldReferences(UpdateTreeEntryCommand request, GameModel game, TreeEntryModel? treeEntry, List<TreeEntryDto> affectedTreeEntries)
        {
            // Re-point EVERY predecessor, not just the first — mirrors the same fix in
            // RemoveTreeEntryCommandHandler for the case where more than one row ends up
            // with Next == treeEntry.Id.
            var oldBefores = game.TreeEntries.Where(x => x.Next?.Id == request.TreeEntryDto.Id).ToList();
            var oldNext = treeEntry.Next;

            if (oldBefores.Count == 0)
            {
                if (oldNext != null)
                    oldNext.Head = true;
                treeEntry.Head = false;
            }
            else
            {
                foreach (var oldBefore in oldBefores)
                {
                    oldBefore.Next = oldNext;
                    affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(oldBefore));
                }
            }

            treeEntry.Next = null;

            if (oldNext != null) affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(oldNext));
        }
    }
}
