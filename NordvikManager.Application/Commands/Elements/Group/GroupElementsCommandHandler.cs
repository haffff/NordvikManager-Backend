using NordvikManager.Application.Commands.Properties.AddProperties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NordvikManager.Application.Commands.Elements.Group
{
    /// <summary>What grouping and ungrouping share.</summary>
    internal static class ElementGrouping
    {
        /// <summary>The group element's property holding its parts' properties, by the parts' ids.</summary>
        public const string GroupedProperties = "grouped_properties";

        public record StoredProperty(string? Name, string? Value, string? Description, bool IsProtected);

        public static bool IsToken(ElementModel element) =>
            element.Properties?.Any(p => p.Name == "isToken" && string.Equals(p.Value, "true", StringComparison.OrdinalIgnoreCase)) == true
            || element.Details?.Any(d => d.Key == "isToken" && string.Equals(d.Value, "true", StringComparison.OrdinalIgnoreCase)) == true;

        public static bool IsGroup(ElementModel element) =>
            element.Details?.Any(d => d.Key == "type" && string.Equals(d.Value, "group", StringComparison.OrdinalIgnoreCase)) == true;

        /// <summary>
        /// The rows a set of elements share: per player (and everyone's row, Guid.Empty), the
        /// rights they have on every one of them. A player without a row of their own on an
        /// element has everyone's row there, or nothing.
        /// </summary>
        public static Dictionary<Guid, Permission?> Shared(IReadOnlyList<Dictionary<Guid, Permission>> rows)
        {
            var players = rows.SelectMany(r => r.Keys).Distinct();
            var shared = new Dictionary<Guid, Permission?>();
            foreach (var player in players)
            {
                var rights = Permission.All;
                foreach (var element in rows)
                {
                    if (element.TryGetValue(player, out var own)) rights &= own;
                    else if (element.TryGetValue(Guid.Empty, out var everyone)) rights &= everyone;
                    else rights = Permission.None;
                }
                shared[player] = rights;
            }
            return shared;
        }

        public static async Task<Dictionary<Guid, Permission>> RowsOf(IDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
            await dbContext.Permissions.Where(p => p.ModelID == id)
                .ToDictionaryAsync(p => p.PlayerID, p => p.Permission, cancellationToken);

        public static async Task SetRows(IDbContext dbContext, Guid id, Dictionary<Guid, Permission?> rows, CancellationToken cancellationToken)
        {
            var existing = await dbContext.Permissions.Where(p => p.ModelID == id).ToListAsync(cancellationToken);
            foreach (var (player, rights) in rows)
            {
                var row = existing.FirstOrDefault(p => p.PlayerID == player);
                if (row == null)
                {
                    row = new PermissionModel { ModelID = id, PlayerID = player, All = player == Guid.Empty };
                    dbContext.Permissions.Add(row);
                }
                row.Permission = rights ?? Permission.None;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    internal class GroupElementsCommandHandler : HandlerBase<GroupElementsCommand, (CommandResponse, Guid)>
    {
        private readonly IMediator mediator;

        public GroupElementsCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid)> Handle(GroupElementsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var playerId = request.Player?.Id ?? Guid.Empty;

            var elements = await dbContext.Elements
                .Include(e => e.Properties)
                .Include(e => e.Details)
                .Include(e => e.Map).ThenInclude(m => m!.Game)
                .AsSplitQuery()
                .Where(e => request.ElementIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            if (elements.Count == 0 || elements.Count != request.ElementIds.Distinct().Count()
                || elements.Any(e => e.Map?.Game?.Id != request.GameId))
                throw WrongArgumentsException.Because("Those aren't elements of a map in this game.");

            foreach (var element in elements)
                element.ThrowIfNoPermission(playerId, Permission.Edit);
            if (elements.Any(ElementGrouping.IsToken))
                throw WrongArgumentsException.Because("Tokens can't be grouped.");

            var rows = new List<Dictionary<Guid, Permission>>();
            var stored = new Dictionary<Guid, List<ElementGrouping.StoredProperty>>();
            foreach (var element in elements)
            {
                rows.Add(await ElementGrouping.RowsOf(dbContext, element.Id, cancellationToken));
                if (element.Properties?.Count > 0)
                    stored[element.Id] = element.Properties.Select(p => new ElementGrouping.StoredProperty(p.Name, p.Value, p.Description, p.IsProtected)).ToList();
            }

            foreach (var element in elements)
                await mediator.Send(new DeleteElementCommand { Id = element.Id, Player = request.Player }, cancellationToken);

            request.Dto.Id = null;
            request.Dto.Properties = new List<PropertyDTO>();
            var (response, groupId) = await mediator.Send(new AddElementCommand { Dto = request.Dto, Player = request.Player, GameID = request.GameId }, cancellationToken);
            if (response != CommandResponse.Ok)
                return (response, groupId);

            if (stored.Count > 0)
            {
                await mediator.Send(new AddPropertiesCommand
                {
                    GameID = request.GameId,
                    Player = request.Player,
                    Properties = new[]
                    {
                        new PropertyDTO
                        {
                            ParentID = groupId, EntityName = "ElementModel",
                            Name = ElementGrouping.GroupedProperties, Value = JsonConvert.SerializeObject(stored),
                        },
                    },
                }, cancellationToken);
            }

            await ElementGrouping.SetRows(dbContext, groupId, ElementGrouping.Shared(rows), cancellationToken);
            return (CommandResponse.Ok, groupId);
        }
    }

    internal class UngroupElementsCommandHandler : HandlerBase<UngroupElementsCommand, (CommandResponse, List<Guid>)>
    {
        private readonly IMediator mediator;

        public UngroupElementsCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, List<Guid>)> Handle(UngroupElementsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var playerId = request.Player?.Id ?? Guid.Empty;

            var group = await dbContext.Elements
                .Include(e => e.Properties)
                .Include(e => e.Details)
                .Include(e => e.Map).ThenInclude(m => m!.Game)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.Id == request.GroupId, cancellationToken);
            if (group == null || group.Map?.Game?.Id != request.GameId || !ElementGrouping.IsGroup(group))
                throw WrongArgumentsException.Because("That isn't a group on a map in this game.");
            group.ThrowIfNoPermission(playerId, Permission.Edit);

            var stored = new Dictionary<Guid, List<ElementGrouping.StoredProperty>>();
            var storedJson = group.Properties?.FirstOrDefault(p => p.Name == ElementGrouping.GroupedProperties)?.Value;
            if (!string.IsNullOrEmpty(storedJson))
            {
                try { stored = JsonConvert.DeserializeObject<Dictionary<Guid, List<ElementGrouping.StoredProperty>>>(storedJson) ?? stored; }
                catch (JsonException) { /* an unreadable stash only loses the parts' properties */ }
            }

            var rights = (await ElementGrouping.RowsOf(dbContext, group.Id, cancellationToken))
                .ToDictionary(x => x.Key, x => (Permission?)x.Value);
            var mapId = group.MapId;
            var layer = group.Layer;

            await mediator.Send(new DeleteElementCommand { Id = group.Id, Player = request.Player }, cancellationToken);

            var ids = new List<Guid>();
            foreach (var child in request.Children)
            {
                var oldId = child.Id;
                child.Id = null;
                child.MapID = mapId;
                child.Layer = layer;
                child.Properties = new List<PropertyDTO>();
                var (response, id) = await mediator.Send(new AddElementCommand { Dto = child, Player = request.Player, GameID = request.GameId }, cancellationToken);
                if (response != CommandResponse.Ok)
                    return (response, ids);
                ids.Add(id);

                if (oldId is Guid old && stored.TryGetValue(old, out var properties) && properties.Count > 0)
                {
                    await mediator.Send(new AddPropertiesCommand
                    {
                        GameID = request.GameId,
                        Player = request.Player,
                        Properties = properties.Select(p => new PropertyDTO
                        {
                            ParentID = id, EntityName = "ElementModel", Name = p.Name, Value = p.Value, IsProtected = p.IsProtected,
                        }).ToArray(),
                    }, cancellationToken);
                }

                await ElementGrouping.SetRows(dbContext, id, rights, cancellationToken);
            }
            return (CommandResponse.Ok, ids);
        }
    }
}
