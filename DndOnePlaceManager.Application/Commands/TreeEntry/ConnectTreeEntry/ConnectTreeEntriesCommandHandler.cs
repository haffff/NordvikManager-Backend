using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry
{
    internal class ConnectTreeEntriesCommandHandler : HandlerBase<ConnectTreeEntriesCommand, CommandResponse>
    {
        private readonly ILogger<ConnectTreeEntriesCommandHandler> logger;
        private readonly IMediator mediator;

        // Every GetTree fetch runs this handler, and it both reads "what's the current
        // tail?" and writes a new link based on that read. Two callers (e.g. two players'
        // clients loading the same panel around the same time) can both read the same
        // tail before either commits, and the second SaveChanges silently drops the
        // first caller's link. Serialize per (game, entityType) so reads and writes for
        // the same chain never interleave across requests.
        private static readonly ConcurrentDictionary<(Guid GameId, string EntityType), SemaphoreSlim> connectLocks = new();

        public ConnectTreeEntriesCommandHandler(IDbContext dbContext, IMapper mapper, ILogger<ConnectTreeEntriesCommandHandler> logger, IMediator mediator) : base(dbContext, mapper)
        {
            this.logger = logger;
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(ConnectTreeEntriesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var gate = connectLocks.GetOrAdd((request.GameID, request.EntityType), _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await ConnectPendingEntries(request, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<CommandResponse> ConnectPendingEntries(ConnectTreeEntriesCommand request, CancellationToken cancellationToken)
        {
            var game = dbContext.Games
                .Include(x => x.TreeEntries).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Next)
                .FirstOrDefault(x => x.Id == request.GameID);

            Guard.Argument(game != null, nameof(request.GameID));

            var newTreeEntries = game.TreeEntries.Where(x => x.NewItem == true && x.EntryType == request.EntityType);
            if (newTreeEntries.Any())
            {
                logger.LogInformation("New tree entries found. Connecting to tree");
            }

            foreach (var entry in newTreeEntries)
            {
                //Find parent
                var parentId = entry.Parent?.Id;

                //Find last entry in the list
                var entryWithoutNext = game.TreeEntries.FirstOrDefault(x => x.Next == null && x.Parent?.Id == parentId && x.NewItem != true && x.EntryType == entry.EntryType);

                //If it exists
                if (entryWithoutNext != null)
                {
                    logger.LogInformation($"Connected {entry.Name} ({entry.Id}) To {entryWithoutNext.Name} ({entryWithoutNext.Id})");
                    //Set next of last entry to new entry
                    entryWithoutNext.Next = entry;
                }
                else
                {
                    entry.Head = true;
                    logger.LogInformation($"Connected {entry.Name} ({entry.Id}) as first child of {parentId}");
                }
                entry.NewItem = false;
            }

            await dbContext.SaveChangesAsync();

            //Check for integrity
            CheckTreeCommand checkTreeCommand = new CheckTreeCommand()
            {
                GameID = request.GameID,
                EntityType = request.EntityType,
                Fix = false
            };

            await mediator.Send(checkTreeCommand);

            return CommandResponse.Ok;
        }
    }
}
