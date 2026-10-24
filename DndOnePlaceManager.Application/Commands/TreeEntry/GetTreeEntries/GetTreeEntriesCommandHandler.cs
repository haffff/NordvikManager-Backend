using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries
{
    internal class GetTreeEntriesCommandHandler : HandlerBase<GetTreeEntriesCommand, List<TreeEntryDto>>
    {
        private readonly IMediator mediator;
        private readonly IPermissionService permissionService;

        public GetTreeEntriesCommandHandler(IDbContext ctx, IMapper mapper, IMediator mediator, IPermissionService permissionService) : base(ctx, mapper)
        {
            this.mediator = mediator;
            this.permissionService = permissionService;
        }

        public override async Task<List<TreeEntryDto>> Handle(GetTreeEntriesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            ConnectTreeEntriesCommand connectTreeEntriesCommand = new ConnectTreeEntriesCommand()
            {
                GameID = request.GameId ?? default,
                EntityType = request.EntityType
            };

            await mediator.Send(connectTreeEntriesCommand, cancellationToken);

            var game = dbContext.Games
                .IncludeTree(request.EntityType)
                .FirstOrDefault(x => request.GameId == x.Id);

            var treeEntries = game
                .TreeEntries
                .Where(x => x.EntryType == request.EntityType)
                .ToList();

            var playerId = request.PlayerId ?? Guid.Empty;
            if (permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                return mapper.Map<List<TreeEntryDto>>(treeEntries);

            return OnlyVisible(treeEntries, VisibleTargets(game.Id, playerId, request.EntityType, treeEntries));
        }

        /// <summary>
        /// The targets of this tree a player who isn't a GM may see, by the same rule as the
        /// list next to it: cards, templates, views and maps they can read, their own resources,
        /// and no playlists (those panels are the GM's).
        /// </summary>
        private HashSet<Guid> VisibleTargets(Guid gameId, Guid playerId, string entryType, List<TreeEntryModel> entries)
        {
            var targets = entries.Where(x => !x.IsFolder && x.TargetId.HasValue).Select(x => x.TargetId!.Value).ToList();
            if (targets.Count == 0)
                return new HashSet<Guid>();

            switch (entryType)
            {
                case TreeEntryTypes.Playlist:
                case TreeEntryTypes.Soundboard:
                    return new HashSet<Guid>();
                case "ResourceModel":
                    return dbContext.Resources
                        .Where(r => r.GameId == gameId && r.PlayerId == playerId && targets.Contains(r.Id))
                        .Select(r => r.Id)
                        .ToHashSet();
                default:
                    return permissionService.GetPermittedIds(playerId, targets, Permission.Read);
            }
        }

        /// <summary>
        /// The entries pointing at visible targets and the folders on the way to them; each
        /// level's siblings linked again (Head, Next) around what was left out, in their order.
        /// </summary>
        private List<TreeEntryDto> OnlyVisible(List<TreeEntryModel> entries, HashSet<Guid> visibleTargets)
        {
            var byId = entries.ToDictionary(x => x.Id);
            var kept = new HashSet<Guid>();
            foreach (var leaf in entries.Where(x => !x.IsFolder && x.TargetId.HasValue && visibleTargets.Contains(x.TargetId.Value)))
            {
                for (var e = leaf; e != null && kept.Add(e.Id); e = e.Parent != null && byId.TryGetValue(e.Parent.Id, out var p) ? p : null) { }
            }

            var result = new List<TreeEntryDto>();
            foreach (var level in entries.GroupBy(x => x.Parent?.Id))
            {
                TreeEntryDto? previous = null;
                foreach (var entry in InOrder(level.ToList()).Where(x => kept.Contains(x.Id)))
                {
                    var dto = mapper.Map<TreeEntryDto>(entry);
                    dto.Next = null;
                    dto.Head = previous == null ? true : null;
                    if (previous != null)
                        previous.Next = dto.Id;
                    result.Add(dto);
                    previous = dto;
                }
            }
            return result;
        }

        /// <summary>One level's siblings in order: from the head along Next, then any not reached.</summary>
        private static IEnumerable<TreeEntryModel> InOrder(List<TreeEntryModel> siblings)
        {
            var seen = new HashSet<Guid>();
            var pointedTo = siblings.Where(x => x.Next != null).Select(x => x.Next!.Id).ToHashSet();
            var starts = siblings.Where(x => !pointedTo.Contains(x.Id)).OrderByDescending(x => x.Head == true);
            foreach (var start in starts)
            {
                for (var e = start; e != null && seen.Add(e.Id); e = e.Next != null ? siblings.FirstOrDefault(x => x.Id == e.Next.Id) : null)
                    yield return e;
            }
            foreach (var e in siblings.Where(x => !seen.Contains(x.Id)))
                yield return e;
        }
    }
}
