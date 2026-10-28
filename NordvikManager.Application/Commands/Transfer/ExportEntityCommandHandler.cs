using NordvikManager.Application.Commands.Resources.GetResourceContent;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.Commands.Transfer
{
    internal class ExportEntityCommandHandler : HandlerBase<ExportEntityCommand, EntityExportDto?>
    {
        private readonly IMediator mediator;

        public ExportEntityCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<EntityExportDto?> Handle(ExportEntityCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;
            var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            game.ThrowIfNoPermission(playerId, Permission.Edit);
            Guard.Argument(EntityTransferKinds.IsKnown(request.Kind), nameof(request.Kind));

            var refs = new FileRefs();
            EntityExportDto? export;
            List<Guid> entityIds;
            if (EntityTransferKinds.IsMap(request.Kind))
                (export, entityIds) = await ExportMap(request, refs, cancellationToken);
            else if (EntityTransferKinds.IsCard(request.Kind))
                (export, entityIds) = await ExportCard(request, refs, cancellationToken);
            else
                (export, entityIds) = await ExportPlaylist(request, refs, cancellationToken);

            if (export == null)
                return null;

            export.Resources = await ExportResources(request, refs, cancellationToken);
            export.Permissions = await dbContext.Permissions
                .Where(p => entityIds.Contains(p.ModelID))
                .Select(p => new ExportedPermissionDto { EntityId = p.ModelID, PlayerId = p.PlayerID, All = p.All, Permission = p.Permission })
                .ToListAsync(cancellationToken);
            return export;
        }

        // Everything on it, hidden elements too: not through GetMap, which leaves out what
        // the asking player can't see.
        private async Task<(EntityExportDto?, List<Guid>)> ExportMap(ExportEntityCommand request, FileRefs refs, CancellationToken cancellationToken)
        {
            var map = await dbContext.Maps
                .Include(x => x.Properties)
                .Include(x => x.Elements!).ThenInclude(x => x.Details)
                .Include(x => x.Elements!).ThenInclude(x => x.Properties)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.Game.Id == request.GameId, cancellationToken);
            if (map == null)
                return (null, new());

            var dto = mapper.Map<MapDTO>(map);
            foreach (var element in dto.Elements ?? Enumerable.Empty<ElementDTO>())
            {
                if (element.Object == null) continue;
                var fabric = JObject.Parse(element.Object);
                refs.AddId((string?)fabric[EntityTransferFields.ResourceId]);
                refs.AddKey((string?)fabric[EntityTransferFields.ResourceKey]);
            }
            foreach (var property in (dto.Properties ?? Enumerable.Empty<PropertyDTO>())
                         .Concat((dto.Elements ?? Enumerable.Empty<ElementDTO>()).SelectMany(e => e.Properties ?? Enumerable.Empty<PropertyDTO>())))
                refs.AddCandidate(property.Value);

            var ids = new List<Guid> { map.Id };
            ids.AddRange(map.Elements?.Select(x => x.Id) ?? Enumerable.Empty<Guid>());
            return (Export(EntityTransferKinds.Map, map.Name, dto), ids);
        }

        private async Task<(EntityExportDto?, List<Guid>)> ExportCard(ExportEntityCommand request, FileRefs refs, CancellationToken cancellationToken)
        {
            var card = await dbContext.Cards
                .Include(x => x.Properties)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.GameId == request.GameId, cancellationToken);
            if (card == null)
                return (null, new());

            var dto = mapper.Map<CardDto>(card);
            refs.AddId(dto.MainResource);
            foreach (var id in dto.AdditionalResources ?? new())
                refs.AddId(id);
            // Any value can name a file (an addon's portrait, a token definition): those
            // that do are found below, the rest are left alone.
            foreach (var property in dto.Properties ?? Enumerable.Empty<PropertyDTO>())
                refs.AddCandidate(property.Value);

            return (Export(EntityTransferKinds.Of(card), card.Name, dto), new List<Guid> { card.Id });
        }

        private async Task<(EntityExportDto?, List<Guid>)> ExportPlaylist(ExportEntityCommand request, FileRefs refs, CancellationToken cancellationToken)
        {
            var playlist = await dbContext.Playlists
                .Include(x => x.Resources)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.GameId == request.GameId, cancellationToken);
            if (playlist == null)
                return (null, new());

            var dto = mapper.Map<PlaylistDTO>(playlist);
            dto.Resources = playlist.Resources
                .Select(r => new ResourceDTO { Id = r.Id, Name = r.Name, Key = r.Key, MimeType = r.MimeType.GetDescriptionValue() })
                .ToList();
            foreach (var resource in playlist.Resources)
                refs.AddId(resource.Id);

            return (Export(EntityTransferKinds.Of(playlist), playlist.Name, dto), new List<Guid> { playlist.Id });
        }

        private static EntityExportDto Export(string kind, string? name, object dto) => new()
        {
            Kind = kind,
            Name = name,
            Data = JObject.FromObject(dto, EntityTransferFields.Serializer),
        };

        private async Task<List<ExportedResourceDto>> ExportResources(ExportEntityCommand request, FileRefs refs, CancellationToken cancellationToken)
        {
            if (refs.Ids.Count == 0 && refs.Keys.Count == 0)
                return new();

            var ids = refs.Ids.ToList();
            var keys = refs.Keys.ToList();
            var resources = await dbContext.Resources
                .Where(r => r.GameId == request.GameId && (ids.Contains(r.Id) || (r.Key != null && keys.Contains(r.Key))))
                .Select(r => new { r.Id, r.Key, r.Name, r.MimeType })
                .ToListAsync(cancellationToken);

            var exported = new List<ExportedResourceDto>();
            foreach (var resource in resources)
            {
                var file = new ExportedResourceDto { Id = resource.Id, Key = resource.Key, Name = resource.Name, MimeType = resource.MimeType.GetDescriptionValue() };
                if (request.IncludeFiles)
                {
                    // Through the storage-aware read: a managed or linked file's bytes aren't in the row.
                    var content = await mediator.Send(new GetResourceContentCommand { GameID = request.GameId, ID = resource.Id, Player = request.Player }, cancellationToken);
                    if (content?.Data != null)
                        file.Data = Convert.ToBase64String(content.Data);
                }
                exported.Add(file);
            }
            return exported;
        }

        /// <summary>The ids and keys an entity may use files by.</summary>
        private sealed class FileRefs
        {
            public HashSet<Guid> Ids { get; } = new();
            public HashSet<string> Keys { get; } = new();

            public void AddId(Guid? id)
            {
                if (id is Guid value && value != Guid.Empty) Ids.Add(value);
            }

            public void AddId(string? id)
            {
                if (Guid.TryParse(id, out var value)) AddId(value);
            }

            public void AddKey(string? key)
            {
                if (!string.IsNullOrWhiteSpace(key)) Keys.Add(key);
            }

            // A value that could be a file's id or key; it counts only if the game has one.
            public void AddCandidate(string? value)
            {
                if (Guid.TryParse(value, out var id)) AddId(id);
                else if (EntityTransferFields.CouldBeKey(value)) Keys.Add(value!);
            }
        }
    }
}
