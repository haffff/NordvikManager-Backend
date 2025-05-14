using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree
{
    public class CheckTreeCommandHandler : HandlerBase<CheckTreeCommand, CommandResponse>
    {
        private readonly IDbContext dbContext;
        private readonly IMapper mapper;
        private readonly ILogger<CheckTreeCommandHandler> logger;

        public CheckTreeCommandHandler(IDbContext dbContext, IMapper mapper, ILogger<CheckTreeCommandHandler> logger) : base(dbContext, mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
            this.logger = logger;
        }

        public override async Task<CommandResponse> Handle(CheckTreeCommand request, CancellationToken cancellationToken)
        {
            base.Handle(request, cancellationToken);

            var game = await dbContext.Games
                .Include(x => x.TreeEntries)
                .FirstOrDefaultAsync(x => x.Id == request.GameID, cancellationToken);

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(GameModel));
            }

            var treeEntries = game.TreeEntries.Where(x => x.EntryType == request.EntityType);
            var groups = treeEntries.GroupBy(x => x.Parent);

            foreach (var group in groups)
            {
                var head = group.FirstOrDefault(x => x.Head == true);
                if (head == null)
                {
                    logger.LogWarning($"No head found for {group.Key?.Name} ({group.Key?.Id}) children!");
                    continue;
                }

                int i = 0;
                var element = head;
                while (element.Next != null)
                {
                    i++;

                    if (element.Next == element)
                    {
                        throw new TreeException($"Infinite loop detected in {element.Name} ({element.Id})");
                    }

                    element = element.Next;
                }

                if (i + 1 != group.Count())
                {
                    logger.LogWarning($"Inconsistency detected in {group.Key?.Name} ({group.Key?.Id}) children!");
                }

                if(request.Fix)
                {
                    FixSubTree(group);
                    dbContext.SaveChanges();
                }
            }

            return CommandResponse.Ok;
        }

        private static void FixSubTree(IGrouping<TreeEntryModel?, TreeEntryModel> group)
        {
            var orderedGroup = group.OrderBy(x => x.Name).ToList();
            bool first = true;
            for (int j = 0; j < orderedGroup.Count; j++)
            {
                if (j == 0)
                {
                    orderedGroup[j].Head = true;
                }

                if (j == orderedGroup.Count - 1)
                {
                    orderedGroup[j].Next = null;
                }
                else
                {
                    orderedGroup[j].Next = orderedGroup[j + 1];
                }

                orderedGroup[j].NewItem = false;
            }
        }
    }
}
