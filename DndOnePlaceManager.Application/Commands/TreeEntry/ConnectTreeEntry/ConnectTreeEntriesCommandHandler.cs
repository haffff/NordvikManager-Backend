using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
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
                .IncludeTree(request.EntityType)
                .FirstOrDefault(x => x.Id == request.GameID);

            Guard.Argument(game != null, nameof(request.GameID));

            AddMissingEntries(game, request.EntityType);

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

        /// <summary>
        /// Templates, custom views, playlists and soundboards made before their panel had
        /// folders have no tree entry. Give each one a pending entry; the loop in
        /// ConnectPendingEntries then links it at the end of the top level. Runs under the
        /// same per-tree lock, and skips anything already filed, so it never duplicates.
        /// </summary>
        private void AddMissingEntries(GameModel game, string entityType)
        {
            var items = ItemsShownIn(game.Id, entityType);
            if (items == null)
                return;

            var filed = game.TreeEntries
                .Where(x => x.EntryType == entityType && x.TargetId != null)
                .Select(x => x.TargetId!.Value)
                .ToHashSet();

            foreach (var (id, name) in items.Where(x => !filed.Contains(x.Id)).OrderBy(x => x.Name))
            {
                var entry = new TreeEntryModel
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    EntryType = entityType,
                    TargetId = id,
                    IsFolder = false,
                    NewItem = true,
                    Game = game,
                };
                // Added explicitly: with a preset key, EF would otherwise treat an entity found
                // through the navigation as already stored and try to update it.
                dbContext.TreeEntries.Add(entry);
                game.TreeEntries.Add(entry);
            }
        }

        /// <summary>The items a tree should contain, for trees that get backfilled; null for the rest.</summary>
        private List<(Guid Id, string Name)>? ItemsShownIn(Guid gameId, string entityType)
        {
            var cards = dbContext.Cards.Where(c => c.GameId == gameId);
            var playlists = dbContext.Playlists.Where(p => p.GameId == gameId);

            var query = entityType switch
            {
                TreeEntryTypes.CardTemplate => cards.Where(c => c.IsTemplate && !c.IsCustomUi).Select(c => new { c.Id, c.Name }),
                TreeEntryTypes.CustomView   => cards.Where(c => c.IsCustomUi).Select(c => new { c.Id, c.Name }),
                TreeEntryTypes.Playlist     => playlists.Where(p => p.Kind == PlaylistKind.Music).Select(p => new { p.Id, p.Name }),
                TreeEntryTypes.Soundboard   => playlists.Where(p => p.Kind == PlaylistKind.Soundboard).Select(p => new { p.Id, p.Name }),
                _ => null,
            };

            return query?.AsEnumerable().Select(x => (x.Id, x.Name)).ToList();
        }
    }
}
