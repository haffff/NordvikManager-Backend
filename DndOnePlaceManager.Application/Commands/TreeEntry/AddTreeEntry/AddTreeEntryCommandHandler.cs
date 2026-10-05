using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Folder.AddFolder
{
    internal class AddTreeEntryCommandHandler : HandlerBase<AddTreeEntryCommand, (CommandResponse, List<TreeEntryDto>)>
    {
        public AddTreeEntryCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }

        public override async Task<(CommandResponse, List<TreeEntryDto>)> Handle(AddTreeEntryCommand request, CancellationToken token)
        {
            await base.Handle(request, token);

            var playerId = request.Player.Id ?? Guid.Empty;

            if (playerId == Guid.Empty)
            {
                throw new ResourceNotFoundException("Player", playerId);
            }

            var treeEntry = mapper.Map<TreeEntryModel>(request.TreeEntryDto);

            // AsSplitQuery() — see InstallAddonCommandHandler.Handle's own comment
            // for why chaining multiple collection .Include()s without it is a
            // cartesian-explosion risk. This one is especially hot: it runs once
            // per resource during an addon install (via AddResourceCommand), so
            // the Players x TreeEntries join cost was being paid dozens of times
            // per install, growing every call as TreeEntries accumulated.
            var game = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.TreeEntries).ThenInclude(y => y.Parent)
                .Include(x => x.TreeEntries).ThenInclude(y => y.Next)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));

            Guard.NotFound(game, "Game", request.GameId);

            if (treeEntry.IsFolder)
            {
                game.ThrowIfNoPermission(playerId, Permission.Edit);
            }

            Guard.Argument(
                !treeEntry.TargetId.HasValue || !game.TreeEntries.Any(x => x.TargetId == treeEntry.TargetId && x.EntryType == treeEntry.EntryType),
                nameof(treeEntry.TargetId), "Duplicate tree entry for this target.");

            // Work out where the entry goes before touching the tree, so a rejected request
            // leaves nothing half-saved behind.
            TreeEntryModel? next = null;
            if (request.TreeEntryDto.Next != null)
            {
                next = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.Next);
                Guard.NotFound(next, "TreeEntry", request.TreeEntryDto.Next);
                Guard.Argument(next.EntryType == treeEntry.EntryType, nameof(request.TreeEntryDto.Next), "Next entry belongs to a different tree.");
            }

            TreeEntryModel? parent = next?.Parent;
            if (request.TreeEntryDto.ParentId != null)
            {
                parent = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.ParentId);
                Guard.NotFound(parent, "TreeEntry", request.TreeEntryDto.ParentId);
                Guard.Argument(next == null || next.Parent?.Id == parent.Id, nameof(request.TreeEntryDto.Next), nameof(request.TreeEntryDto.ParentId));
            }

            if (request.TreeEntryDto.AutoConnect == true && next?.NewItem == true)
            {
                // Items get their entry pending and are linked on the next tree load; one the
                // user can already see (and insert before) gets linked now instead.
                LinkPendingEntries(game, treeEntry.EntryType, parent);
            }

            game.TreeEntries.Add(treeEntry);
            treeEntry.Parent = parent;
            treeEntry.NewItem = true;

            if (request.TreeEntryDto.AutoConnect == true)
            {
                return await ConnectTreeEntry(request, treeEntry, game);
            }

            dbContext.SaveChanges();

            return (CommandResponse.Ok, new List<TreeEntryDto>() { mapper.Map<TreeEntryDto>(treeEntry) });
        }

        /// <summary>Appends the pending entries of one tree level after its last linked entry, like ConnectTreeEntries does.</summary>
        private static void LinkPendingEntries(GameModel game, string entryType, TreeEntryModel? parent)
        {
            var pending = game.TreeEntries
                .Where(x => x.NewItem == true && x.EntryType == entryType && x.Parent?.Id == parent?.Id)
                .ToList();

            foreach (var entry in pending)
            {
                var tail = game.TreeEntries.FirstOrDefault(x => x.Next == null && x.NewItem != true && x.EntryType == entryType && x.Parent?.Id == parent?.Id);
                if (tail != null)
                    tail.Next = entry;
                else
                    entry.Head = true;
                entry.NewItem = false;
            }
        }

        private async Task<(CommandResponse, List<TreeEntryDto>)> ConnectTreeEntry(AddTreeEntryCommand request, TreeEntryModel treeEntry, GameModel game)
        {
            TreeEntryModel entryWithNext = null;
            TreeEntryModel first = null;

            var treeEntries = game.TreeEntries.Where(x => x.NewItem == false && x.EntryType == request.TreeEntryDto.EntryType);

            if (!treeEntries.Any(x => x.Parent == treeEntry.Parent))
            {
                Guard.Argument(request.TreeEntryDto.Next == null, nameof(request.TreeEntryDto.Next), nameof(request.TreeEntryDto.ParentId));
                treeEntry.Head = true;
            }

            //If next exists
            if (request.TreeEntryDto.Next != null)
            {
                //Find next entry
                var next = await dbContext.TreeEntries.FirstOrDefaultAsync(x => x.Id == request.TreeEntryDto.Next);

                Guard.NotFound(next, "TreeEntry", request.TreeEntryDto.Next);

                //Find entry before
                entryWithNext = treeEntries.FirstOrDefault(x => x.Next?.Id == request.TreeEntryDto.Next);

                //If it exists
                if (entryWithNext != null)
                {
                    entryWithNext.Next = treeEntry;
                }
                else
                {
                    //If it doesn't exist, this is new head
                    next.Head = false;
                    treeEntry.Head = true;
                }

                treeEntry.Next = next;
            }
            else
            {
                //if next not exists
                first = treeEntries.LastOrDefault(x => x.Next == null && x.Parent == treeEntry.Parent && x != treeEntry && x.NewItem != true);
                if (first != null)
                {
                    first.Next = treeEntry;
                }
            }
            dbContext.SaveChanges();

            var mainEntry = mapper.Map<TreeEntryDto>(treeEntry);
            mainEntry.AutoConnect = true;
            List<TreeEntryDto> entriesAffected = new List<TreeEntryDto>
                {
                    mainEntry
                };

            if (entryWithNext != null)
            {
                entriesAffected.Add(mapper.Map<TreeEntryDto>(entryWithNext));
            }

            if (first != null)
            {
                entriesAffected.Add(mapper.Map<TreeEntryDto>(first));
            }

            treeEntry.NewItem = false;

            dbContext.SaveChanges();

            return (CommandResponse.Ok, entriesAffected);
        }
    }
}
