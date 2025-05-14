using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Folder.AddFolder
{
    internal class AddTreeEntryCommandHandler : HandlerBase<AddTreeEntryCommand, (CommandResponse,List<TreeEntryDto>)>
    {
        public AddTreeEntryCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }

        public override async Task<(CommandResponse, List<TreeEntryDto>)> Handle(AddTreeEntryCommand request, CancellationToken token)
        {
            await base.Handle(request, token);

            var playerId = request.Player.Id ?? Guid.Empty;

            if(playerId == Guid.Empty)
            {
                throw new ResourceNotFoundException(nameof(PlayerModel));
            }

            var treeEntry = mapper.Map<TreeEntryModel>(request.TreeEntryDto);

            var game = await dbContext.Games
                .Include(x=>x.Players)
                .Include(x=>x.TreeEntries).ThenInclude(y=>y.Parent)
                .Include(x=>x.TreeEntries).ThenInclude(y=>y.Next)
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            game.TreeEntries.Add(treeEntry);

            if(request.TreeEntryDto.ParentId == null && request.TreeEntryDto.Next != null)
            {
                var next = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.Next);
                treeEntry.Parent = next.Parent;
            }
                 
            //assign to folder
            if (request.TreeEntryDto.ParentId != null && treeEntry.Parent == null)
            {
                var parent = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.ParentId);

                if (parent == null)
                {
                    throw new ResourceNotFoundException(nameof(parent));
                }


                treeEntry.Parent = parent;
            }

            treeEntry.NewItem = true;

            dbContext.SaveChanges();

            if(request.TreeEntryDto.AutoConnect == true)
            {
                return await ConnectTreeEntry(request, treeEntry, game);
            }

            return (CommandResponse.Ok, new List<TreeEntryDto>() { mapper.Map<TreeEntryDto>(treeEntry) });
        }

        private async Task<(CommandResponse, List<TreeEntryDto>)> ConnectTreeEntry(AddTreeEntryCommand request, TreeEntryModel treeEntry, GameModel game)
        {
            TreeEntryModel entryWithNext = null;
            TreeEntryModel first = null;

            var treeEntries = game.TreeEntries.Where(x => x.NewItem == false && x.EntryType == request.TreeEntryDto.EntryType);

            if(!treeEntries.Any(x=>x.Parent == treeEntry.Parent))
            {
                if(request.TreeEntryDto.Next != null)
                {
                    throw new WrongArgumentsException(nameof(request.TreeEntryDto.Next), nameof(request.TreeEntryDto.ParentId));
                }
                treeEntry.Head = true;
            }

            //If next exists
            if (request.TreeEntryDto.Next != null)
            {
                //Find next entry
                var next = await dbContext.TreeEntries.FirstOrDefaultAsync(x => x.Id == request.TreeEntryDto.Next);

                if (next == null)
                {
                    throw new ResourceNotFoundException(nameof(request.TreeEntryDto.Next));
                }

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
