using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Exceptions;
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

            var game = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.TreeEntries.Where(x=>x.NewItem != true)).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries.Where(x => x.NewItem != true)).ThenInclude(x => x.Next)
                .FirstOrDefaultAsync(x => request.GameId == x.Id && x.Players.Any(x => x.Id == playerId));

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(GameModel));
            }

            var treeEntry = game.TreeEntries.FirstOrDefault(x => x.Id == request.TreeEntryDto.Id);
            if (treeEntry == null)
            {
                throw new ResourceNotFoundException(nameof(TreeEntryModel));
            }

            treeEntry.Name = request.TreeEntryDto.Name ?? treeEntry.Name;
            treeEntry.Color = request.TreeEntryDto.Color ?? treeEntry.Color;
            treeEntry.Icon = request.TreeEntryDto.Icon ?? treeEntry.Icon;

            List<TreeEntryDto> affectedTreeEntries = new List<TreeEntryDto>();

            if(request.TreeEntryDto.Next == treeEntry.Next?.Id && treeEntry.Parent?.Id == request.TreeEntryDto.ParentId)
            {
                dbContext.SaveChanges();
                return (CommandResponse.Ok, new List<TreeEntryDto>() { mapper.Map<TreeEntryDto>(treeEntry) });
            }

            if(request.TreeEntryDto.ParentId == request.TreeEntryDto.Id || request.TreeEntryDto.Next == request.TreeEntryDto.Id)
            {
                throw new WrongArgumentsException(nameof(request.TreeEntryDto.ParentId), nameof(request.TreeEntryDto.Next));
            }

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

            if (nextModel == null && next != null)
            {
                return (CommandResponse.WrongArguments, new List<TreeEntryDto>());
            }

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
            var oldBefore = game.TreeEntries.FirstOrDefault(x => x.Next?.Id == request.TreeEntryDto.Id);
            var oldNext = treeEntry.Next;

            if (oldBefore == null)
            {
                if(oldNext != null)
                    oldNext.Head = true;
                treeEntry.Head = false;
            }
            else
            {
                oldBefore.Next = oldNext;
            }

            treeEntry.Next = null;

            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(oldBefore));
            affectedTreeEntries.Add(mapper.Map<TreeEntryDto>(oldNext));
        }
    }
}
