using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Map.AddMap;
using NordvikManager.Application.Commands.Playlist.AddPlaylist;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Commands.TreeEntry.EnsureFolderPath;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.Commands.Transfer
{
    internal class ImportEntityCommandHandler : HandlerBase<ImportEntityCommand, ImportEntityResult>
    {
        private readonly IMediator mediator;

        public ImportEntityCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<ImportEntityResult> Handle(ImportEntityCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;
            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            game.ThrowIfNoPermission(playerId, Permission.Edit);

            var export = request.Export;
            if (export == null || export.Format != EntityExportDto.FormatName || export.Data == null)
                throw WrongArgumentsException.Because("This isn't an exported map, card or playlist.");
            if (export.Version < 1 || export.Version > EntityExportDto.CurrentVersion)
                throw WrongArgumentsException.Because("This file was exported by a newer version of NordvikManager.");
            if (!EntityTransferKinds.IsKnown(export.Kind))
                throw WrongArgumentsException.Because($"Can't import a \"{export.Kind}\".");
            if (request.ExpectedKind != null && request.ExpectedKind != export.Kind)
                throw WrongArgumentsException.Because($"This file holds a {export.Kind}, not a {request.ExpectedKind}.");

            var entryType = EntityTransferKinds.TreeEntryType(export.Kind);
            var treeParentId = request.TreeParentId;
            var newFolders = new List<TreeEntryDto>();
            if (treeParentId == null && !string.IsNullOrWhiteSpace(request.FolderPath))
            {
                (treeParentId, newFolders) = await mediator.Send(new EnsureFolderPathCommand
                {
                    GameID = request.GameId, Player = request.Player, EntryType = entryType, Path = request.FolderPath,
                }, cancellationToken);
            }

            var import = new Import(request, game, treeParentId, new FileResolver(dbContext, mediator, request));
            Guid id;
            if (EntityTransferKinds.IsMap(export.Kind))
                id = await ImportMap(import, cancellationToken);
            else if (EntityTransferKinds.IsCard(export.Kind))
                id = await ImportCard(import, cancellationToken);
            else
                id = await ImportPlaylist(import, cancellationToken);

            await CopyPermissions(import, cancellationToken);
            var entry = await PlaceInTree(import, entryType, id, cancellationToken);

            var missing = import.Files.Missing.Concat(import.Cards.SelectMany(c => c.Missing)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            return new ImportEntityResult(id, export.Kind, entry, missing, newFolders, import.Cards);
        }

        // ── Maps ─────────────────────────────────────────────────────────────────

        private async Task<Guid> ImportMap(Import import, CancellationToken cancellationToken)
        {
            var dto = import.Request.Export.Data.ToObject<MapDTO>(EntityTransferFields.Serializer)!;
            var elements = dto.Elements?.ToList() ?? new();

            // New ids first, so whatever refers to one (a fabric object's id, a property's
            // parent) can be pointed at the copy.
            await ImportTokenCards(import, dto.Name ?? import.Request.Export.Name, cancellationToken);

            var mapId = import.NewId(dto.Id);
            var elementIds = elements.Select(e => import.NewId(e.Id)).ToList();

            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var id = elementIds[i];
                element.Id = id;
                element.MapID = mapId;
                element.Permission = null;
                element.Object = await ImportFabricObject(import, element.Object, cancellationToken);
                element.Properties = await WithFiles(import, NewProperties(import, element.Properties, id), cancellationToken);
            }

            dto.Id = mapId;
            dto.GameID = null;
            dto.Permission = null;
            dto.Name = import.Request.Name ?? dto.Name;
            dto.Elements = elements;
            dto.Properties = await WithFiles(import, NewProperties(import, dto.Properties, mapId), cancellationToken);

            var (_, newId) = await mediator.Send(new AddMapCommand
            {
                Dto = dto, GameID = import.Request.GameId, Player = import.Request.Player, TreeParentId = import.TreeParentId,
            }, cancellationToken);

            // The map gets the usual rights from AddMap; its elements the ones a new element gets.
            await SetPermissions(elements.SelectMany(e => DefaultRows(import, e.Id!.Value)), cancellationToken);
            return newId;
        }

        // The cards of the map's tokens, when the export has them: the game's own if it has
        // them (a duplicate), otherwise new ones in a Cards folder named after the map. The
        // tokens are pointed at them below, as their ids are.
        private async Task ImportTokenCards(Import import, string? mapName, CancellationToken cancellationToken)
        {
            foreach (var card in import.Request.Export.Cards ?? new())
            {
                if (!Guid.TryParse((string?)card.Data?["id"], out var oldId))
                    continue;
                if (await dbContext.Cards.AnyAsync(c => c.Id == oldId && c.GameId == import.Request.GameId, cancellationToken))
                    continue;

                var result = await mediator.Send(new ImportEntityCommand
                {
                    GameId = import.Request.GameId,
                    Player = import.Request.Player,
                    Export = card,
                    ExpectedKind = EntityTransferKinds.Card,
                    FolderPath = string.IsNullOrWhiteSpace(mapName) ? null : mapName.Replace("/", " "),
                }, cancellationToken);
                import.PointAt(oldId, result.Id);
                import.Cards.Add(result);
            }
        }

        private async Task<string?> ImportFabricObject(Import import, string? json, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            var fabric = JObject.Parse(json);
            import.PointAtCopies(fabric);

            var resourceId = (string?)fabric[EntityTransferFields.ResourceId];
            var resourceKey = (string?)fabric[EntityTransferFields.ResourceKey];
            var hadPicture = !string.IsNullOrEmpty(resourceId) || !string.IsNullOrEmpty(resourceKey);
            var shown = false;

            if (Guid.TryParse(resourceId, out var oldResource))
            {
                var found = await import.Files.IdAsync(oldResource, cancellationToken);
                if (found != null)
                {
                    fabric[EntityTransferFields.ResourceId] = found.Value.ToString();
                    shown = true;
                }
                else
                {
                    fabric.Remove(EntityTransferFields.ResourceId);
                }
            }
            if (!shown && !string.IsNullOrEmpty(resourceKey))
            {
                if (await import.Files.KeyAsync(resourceKey, cancellationToken))
                    shown = true;
                else
                    fabric.Remove(EntityTransferFields.ResourceKey);
            }
            if (hadPicture && !shown)
            {
                // A token without its picture shows the empty token picture by itself; any
                // other image would show nothing, so it gets the empty image.
                fabric.Remove(EntityTransferFields.ResourceId);
                if (IsTrue(fabric[EntityTransferFields.IsToken]))
                    fabric.Remove(EntityTransferFields.ResourceKey);
                else
                    fabric[EntityTransferFields.ResourceKey] = EntityTransferFields.EmptyImageKey;
            }

            if (Guid.TryParse((string?)fabric[EntityTransferFields.CardId], out var cardId)
                && !await dbContext.Cards.AnyAsync(c => c.Id == cardId && c.GameId == import.Request.GameId, cancellationToken))
            {
                fabric.Remove(EntityTransferFields.CardId);
            }

            return fabric.ToString(Formatting.None);
        }

        private static bool IsTrue(JToken? token) =>
            token != null && (token.Type == JTokenType.Boolean ? (bool)token : string.Equals((string?)token, "true", StringComparison.OrdinalIgnoreCase));

        // ── Cards ────────────────────────────────────────────────────────────────

        private async Task<Guid> ImportCard(Import import, CancellationToken cancellationToken)
        {
            var dto = import.Request.Export.Data.ToObject<CardDto>(EntityTransferFields.Serializer)!;
            var kind = import.Request.Export.Kind;
            var gameId = import.Request.GameId;
            var oldId = dto.Id;

            dto.Id = null;
            dto.TemplateId = null;
            dto.Owner = null;
            dto.Permission = dto.GenericPermission = dto.GmPermission = null;
            dto.Name = import.Request.Name ?? dto.Name;
            // Keys are unique in a game: a copy next to the original goes without.
            if (dto.Key != null && await dbContext.Cards.AnyAsync(c => c.GameId == gameId && c.Key == dto.Key, cancellationToken))
                dto.Key = null;

            dto.MainResource = dto.MainResource is Guid main ? await import.Files.IdAsync(main, cancellationToken) : null;
            var additional = new List<Guid>();
            foreach (var resource in dto.AdditionalResources ?? new())
                if (await import.Files.IdAsync(resource, cancellationToken) is Guid found)
                    additional.Add(found);
            dto.AdditionalResources = additional;

            var properties = new List<PropertyDTO>();
            foreach (var property in dto.Properties ?? Enumerable.Empty<PropertyDTO>())
            {
                if (property.Name == EntityTransferFields.PlayerOwner)
                {
                    // The card stays theirs if they play here too.
                    if (!Guid.TryParse(property.Value, out var owner) || !import.Players.Contains(owner))
                        continue;
                    dto.Owner = owner;
                }
                else if (!await ImportFileValue(import, property, cancellationToken))
                {
                    continue;
                }
                property.Id = null;
                property.ParentID = null;
                property.Permission = null;
                properties.Add(property);
            }
            dto.Properties = properties;

            var (_, id) = await mediator.Send(new AddCardCommand
            {
                Dto = dto,
                GameID = gameId,
                Player = import.Request.Player,
                IsTemplate = kind == EntityTransferKinds.Template,
                IsCustomUi = kind == EntityTransferKinds.View,
                TreeParentId = import.TreeParentId,
            }, cancellationToken);

            if (oldId is Guid old)
                import.Ids[old] = id;
            return id;
        }

        /// <summary>
        /// A property naming one of the exported files points at this game's copy of it;
        /// false if the file isn't here, so the property is left out (the card falls back
        /// to its default, e.g. the empty token picture). Other values are kept as they are.
        /// </summary>
        private static async Task<bool> ImportFileValue(Import import, PropertyDTO property, CancellationToken cancellationToken)
        {
            if (Guid.TryParse(property.Value, out var id))
            {
                if (!import.Files.Lists(id))
                    return true;
                var found = await import.Files.IdAsync(id, cancellationToken);
                if (found == null)
                    return false;
                property.Value = found.Value.ToString();
                return true;
            }
            if (EntityTransferFields.CouldBeKey(property.Value) && import.Files.ListsKey(property.Value!))
                return await import.Files.KeyAsync(property.Value!, cancellationToken);
            return true;
        }

        /// <summary>The properties, with those naming a file this game lacks left out.</summary>
        private static async Task<List<PropertyDTO>> WithFiles(Import import, List<PropertyDTO> properties, CancellationToken cancellationToken)
        {
            var kept = new List<PropertyDTO>();
            foreach (var property in properties)
                if (await ImportFileValue(import, property, cancellationToken))
                    kept.Add(property);
            return kept;
        }

        // ── Playlists ────────────────────────────────────────────────────────────

        private async Task<Guid> ImportPlaylist(Import import, CancellationToken cancellationToken)
        {
            var dto = import.Request.Export.Data.ToObject<PlaylistDTO>(EntityTransferFields.Serializer)!;

            var found = new Dictionary<Guid, Guid>();
            foreach (var resource in dto.Resources ?? new())
                if (resource.Id is Guid old && await import.Files.IdAsync(old, cancellationToken) is Guid here)
                    found[old] = here;

            var (_, id) = await mediator.Send(new AddPlaylistCommand
            {
                GameId = import.Request.GameId,
                Player = import.Request.Player,
                Name = import.Request.Name ?? dto.Name,
                Description = dto.Description,
                Mode = dto.Mode,
                Shuffle = dto.Shuffle,
                Repeat = dto.Repeat,
                Kind = EntityTransferKinds.PlaylistKindOf(import.Request.Export.Kind),
                Volume = dto.Volume,
                ResourceIds = found.Values.Distinct().ToList(),
                LoopedResourceIds = (dto.LoopedResourceIds ?? new()).Where(found.ContainsKey).Select(x => found[x]).Distinct().ToList(),
                TreeParentId = import.TreeParentId,
            }, cancellationToken);

            import.Ids[dto.Id] = id;
            return id;
        }

        // ── Shared ───────────────────────────────────────────────────────────────

        private static List<PropertyDTO> NewProperties(Import import, IEnumerable<PropertyDTO>? properties, Guid parentId) =>
            (properties ?? Enumerable.Empty<PropertyDTO>()).Select(p =>
            {
                p.Id = Guid.NewGuid();
                p.ParentID = parentId;
                p.Permission = null;
                p.Value = import.PointAtCopy(p.Value);
                return p;
            }).ToList();

        // What GenericAddHandler gives anything new: everyone sees it, its maker, the
        // system and the game's owner do anything with it.
        private static IEnumerable<PermissionModel> DefaultRows(Import import, Guid modelId)
        {
            var playerId = import.Request.Player.Id ?? Guid.Empty;
            yield return new PermissionModel { ModelID = modelId, PlayerID = Guid.Empty, All = true, Permission = Permission.Read };
            yield return new PermissionModel { ModelID = modelId, PlayerID = playerId, Permission = Permission.All };
            yield return new PermissionModel { ModelID = modelId, PlayerID = import.Game.SystemPlayerId, Permission = Permission.All };
            if (import.Game.MasterId != Guid.Empty && import.Game.MasterId != playerId)
                yield return new PermissionModel { ModelID = modelId, PlayerID = import.Game.MasterId, Permission = Permission.All };
        }

        // The exported rights, for the copies, of whoever plays in this game. The one
        // importing and the system keep everything on what they made.
        private Task CopyPermissions(Import import, CancellationToken cancellationToken)
        {
            var importer = import.Request.Player.Id ?? Guid.Empty;
            var rows = import.Request.Export.Permissions
                .Where(p => import.Ids.ContainsKey(p.EntityId))
                .Where(p => p.All ? p.PlayerId == Guid.Empty : import.Players.Contains(p.PlayerId))
                .Where(p => p.PlayerId != importer && p.PlayerId != import.Game.SystemPlayerId)
                .Select(p => new PermissionModel { ModelID = import.Ids[p.EntityId], PlayerID = p.PlayerId, All = p.All, Permission = p.Permission });
            return SetPermissions(rows, cancellationToken);
        }

        private async Task SetPermissions(IEnumerable<PermissionModel> rows, CancellationToken cancellationToken)
        {
            var list = rows.ToList();
            if (list.Count == 0)
                return;

            var models = list.Select(x => x.ModelID).Distinct().ToList();
            var existing = await dbContext.Permissions.Where(p => models.Contains(p.ModelID)).ToListAsync(cancellationToken);
            foreach (var row in list)
            {
                var current = existing.FirstOrDefault(p => p.ModelID == row.ModelID && p.PlayerID == row.PlayerID);
                if (current == null)
                {
                    dbContext.Permissions.Add(row);
                    existing.Add(row);
                }
                else
                {
                    current.All = row.All;
                    current.Permission = row.Permission;
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // The new entry is linked at the end of its folder when the tree is next loaded;
        // after a given entry (the original, for a duplicate) it's linked there now.
        private async Task<TreeEntryDto?> PlaceInTree(Import import, string entryType, Guid id, CancellationToken cancellationToken)
        {
            var gameId = import.Request.GameId;
            var entry = await dbContext.TreeEntries
                .Include(x => x.Parent)
                .Include(x => x.Next)
                .FirstOrDefaultAsync(x => x.TargetId == id && x.EntryType == entryType && x.Game.Id == gameId, cancellationToken);
            if (entry == null)
                return null;

            var afterId = import.Request.AfterTreeEntryId;
            var afterTarget = afterId == null ? import.Request.AfterTargetId : null;
            if (afterId != null || afterTarget != null)
            {
                var after = await dbContext.TreeEntries
                    .Include(x => x.Parent)
                    .Include(x => x.Next)
                    .Where(x => x.EntryType == entryType && x.Game.Id == gameId && x.NewItem != true)
                    .FirstOrDefaultAsync(x => afterId != null ? x.Id == afterId : x.TargetId == afterTarget, cancellationToken);
                if (after != null)
                {
                    entry.Parent = after.Parent;
                    entry.Next = after.Next;
                    entry.Head = false;
                    entry.NewItem = false;
                    after.Next = entry;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return mapper.Map<TreeEntryDto>(entry);
        }

        /// <summary>One import: the request, its game and the ids given to the copies.</summary>
        private sealed class Import
        {
            public Import(ImportEntityCommand request, GameModel game, Guid? treeParentId, FileResolver files)
            {
                Request = request;
                Game = game;
                TreeParentId = treeParentId;
                Files = files;
                Players = game.Players.Select(p => p.Id).ToHashSet();
            }

            public ImportEntityCommand Request { get; }
            public GameModel Game { get; }
            public Guid? TreeParentId { get; }
            public FileResolver Files { get; }
            public HashSet<Guid> Players { get; }

            /// <summary>Exported id → the copy's id.</summary>
            public Dictionary<Guid, Guid> Ids { get; } = new();
            private readonly Dictionary<string, string> idTexts = new(StringComparer.OrdinalIgnoreCase);

            public Guid NewId(Guid? old)
            {
                var id = Guid.NewGuid();
                if (old is Guid value && value != Guid.Empty)
                {
                    Ids[value] = id;
                    idTexts[value.ToString()] = id.ToString();
                }
                return id;
            }

            /// <summary>Cards made for a map's tokens.</summary>
            public List<ImportEntityResult> Cards { get; } = new();

            /// <summary>Ids that were <paramref name="old"/> now mean <paramref name="copy"/> (e.g. a token's card).</summary>
            public void PointAt(Guid old, Guid copy) => idTexts[old.ToString()] = copy.ToString();

            /// <summary>A value that is the id of something copied becomes the copy's id.</summary>
            public string? PointAtCopy(string? value) =>
                value != null && idTexts.TryGetValue(value, out var copy) ? copy : value;

            /// <summary>Every string in a fabric object that is a copied id (its own id, a parent's) becomes the copy's.</summary>
            public void PointAtCopies(JContainer fabric)
            {
                foreach (var value in fabric.DescendantsAndSelf().OfType<JValue>().Where(v => v.Type == JTokenType.String).ToList())
                {
                    var text = (string?)value.Value;
                    if (text != null && idTexts.TryGetValue(text, out var copy))
                        value.Value = copy;
                }
            }
        }

        /// <summary>
        /// Finds the files an import uses in its game: by id, then by key, then from the copy
        /// in the export (once, even if used twice, and not again if the game already has
        /// the same file). Names what it can't find.
        /// </summary>
        private sealed class FileResolver
        {
            private readonly IDbContext dbContext;
            private readonly IMediator mediator;
            private readonly ImportEntityCommand request;
            private readonly Dictionary<Guid, ExportedResourceDto> byId = new();
            private readonly Dictionary<string, ExportedResourceDto> byKey = new();
            private readonly Dictionary<Guid, Guid?> found = new();
            private readonly SortedSet<string> missing = new(StringComparer.Ordinal);

            public FileResolver(IDbContext dbContext, IMediator mediator, ImportEntityCommand request)
            {
                this.dbContext = dbContext;
                this.mediator = mediator;
                this.request = request;
                foreach (var file in request.Export.Resources ?? new())
                {
                    byId.TryAdd(file.Id, file);
                    if (!string.IsNullOrEmpty(file.Key))
                        byKey.TryAdd(file.Key, file);
                }
            }

            public IEnumerable<string> Missing => missing;

            public bool Lists(Guid id) => byId.ContainsKey(id);
            public bool ListsKey(string key) => byKey.ContainsKey(key);

            /// <summary>This game's file for an exported file id; null if it has none.</summary>
            public async Task<Guid?> IdAsync(Guid id, CancellationToken cancellationToken)
            {
                if (found.TryGetValue(id, out var known))
                    return known;

                Guid? result = null;
                byId.TryGetValue(id, out var file);
                if (await dbContext.Resources.AnyAsync(r => r.Id == id && r.GameId == request.GameId, cancellationToken))
                    result = id;
                else if (file != null && !string.IsNullOrEmpty(file.Key))
                    result = await ByKey(file.Key, cancellationToken);

                if (result == null && file != null)
                {
                    result = await Create(file, cancellationToken);
                    if (result == null)
                        missing.Add(file.Name);
                }

                found[id] = result;
                return result;
            }

            /// <summary>Whether this game has (or now got from the export) a file under a key. Keys
            /// the export doesn't list are kept: the game's own defaults (emptyTokenImage).</summary>
            public async Task<bool> KeyAsync(string key, CancellationToken cancellationToken)
            {
                if (await ByKey(key, cancellationToken) != null)
                    return true;
                if (!byKey.TryGetValue(key, out var file))
                    return true;
                return await IdAsync(file.Id, cancellationToken) != null;
            }

            private Task<Guid?> ByKey(string key, CancellationToken cancellationToken) =>
                dbContext.Resources
                    .Where(r => r.GameId == request.GameId && r.Key == key)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            private async Task<Guid?> Create(ExportedResourceDto file, CancellationToken cancellationToken)
            {
                if (string.IsNullOrEmpty(file.Data))
                    return null;

                byte[] data;
                try
                {
                    data = Convert.FromBase64String(file.Data);
                }
                catch (FormatException)
                {
                    return null;
                }

                // Imported before: the same file under the same name.
                var hash = ResourceVersions.HashOf(data);
                var same = await dbContext.Resources
                    .Where(r => r.GameId == request.GameId && r.ContentHash == hash && r.Name == file.Name)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (same != null)
                    return same;

                var (response, id) = await mediator.Send(new CreateResourceCommand
                {
                    GameId = request.GameId,
                    Player = request.Player,
                    Key = file.Key,
                    Name = file.Name,
                    Data = data,
                    MimeType = file.MimeType,
                }, cancellationToken);
                return response == CommandResponse.Ok ? id : null;
            }
        }
    }
}
