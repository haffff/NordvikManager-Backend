using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

namespace DndOnePlaceManager.Application.Commands.Addons.InstallAddon
{
    internal class InstallAddonCommandHandler : HandlerBase<InstallAddonCommand, (CommandResponse, InstallAddonCommandResponse)>
    {
        private readonly IDbContext dbContext;
        private readonly IMapper mapper;
        private readonly IAddonRepositoryService addonFromUriProvider;
        private readonly IMediator mediator;
        private readonly IGameEventLogger gameEventLogger;

        public InstallAddonCommandHandler(IDbContext dbContext, IMapper mapper, IAddonRepositoryService addonFromUriProvider, IMediator mediator, IGameEventLogger gameEventLogger) : base(dbContext, mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
            this.addonFromUriProvider = addonFromUriProvider;
            this.mediator = mediator;
            this.gameEventLogger = gameEventLogger;
        }

        public override async Task<(CommandResponse, InstallAddonCommandResponse)> Handle(InstallAddonCommand request, CancellationToken cancellationToken)
        {
            // Only the addons list. Resources, tree entries, cards and actions used to be loaded
            // here too (all of them, resource file bytes included) — on a game with a big
            // addon that was tens of thousands of rows per install, and once failed outright
            // with SQLite "disk full". Everything below looks up just what it needs instead.
            var game = dbContext.Games
                .Include(x => x.Addons)
                .FirstOrDefault(x => x.Id == request.GameID);

            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            // Bug fix: both null would silently fall through to ZipArchive(null) crash
            if (request.AddonFile == null && request.AddonSourceKey == null)
                throw new ArgumentException("Either AddonFile or AddonSourceKey must be provided.");

            gameEventLogger.Info("AddonInstall", $"Installing addon for game '{game.Name}' (ID: {game.Id}) by player '{request.Player.Name}' (ID: {request.Player.Id}).");
            
            if (request.AddonFile == null)
            {
                request.AddonFile = await addonFromUriProvider.GetAddonByKey(request.AddonSourceKey!);
                gameEventLogger.Info("AddonInstall", $"Fetched addon file from source key '{request.AddonSourceKey}");
            }

            using var archive = new ZipArchive(new MemoryStream(request.AddonFile));

            // Bug fix: GetEntry can return null — was crashing in ReadToBytes
            var infoEntry = archive.Entries.FirstOrDefault(x => x.Name.ToLower().Trim() == "info.json")
                ?? throw new InvalidOperationException("Addon archive is missing 'info.json'. Is this a valid addon file?");

            var addon = JsonSerializer.Deserialize<AddonModel>(ReadToBytes(infoEntry), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Cannot deserialize 'info.json'. Are you sure this is a valid addon file?");

            if (addon.Key == null)
                throw new InvalidOperationException("Addon 'info.json' is missing the required 'key' field.");

            // Without this, installing an addon that's already present for this game (e.g. it
            // was pulled in as another addon's dependency, then also selected directly — see
            // FindAndInstallDependencies, which only guards its OWN recursive calls, not a
            // top-level request like this one) silently created a second AddonModel row and
            // re-ran AddActions/AddTemplates/AddViews with no existence check of their own,
            // duplicating every action and card. Two AddonModel rows for the same Key each got
            // their own Hook.Install fired later, and since CallHookAsync queries actions
            // game-wide rather than per-addon, that ran every duplicated action multiple times.
            var existingAddon = game.Addons.FirstOrDefault(x => x.Key == addon.Key);
            if (existingAddon != null)
            {
                if (!request.Reinstall)
                {
                    gameEventLogger.Info("AddonInstall", $"Addon '{addon.Key}' is already installed for game '{game.Name}' — skipping (pass Reinstall to replace it).");
                    return (CommandResponse.AlreadyExists, new InstallAddonCommandResponse
                    {
                        AddonId = existingAddon.Id,
                        AddonKey = existingAddon.Key,
                        AddonName = existingAddon.Name,
                        AddonVersion = existingAddon.Version,
                    });
                }

                gameEventLogger.Info("AddonInstall", $"Addon '{addon.Key}' is already installed for game '{game.Name}' — removing the existing install before reinstalling.");
                await mediator.Send(new UninstallAddonCommand
                {
                    Player = request.Player,
                    GameID = request.GameID,
                    AddonId = existingAddon.Id,
                });
                game.Addons.Remove(existingAddon);
            }

            // Reset navigation collections so EF doesn't try to re-attach stale entries
            addon.Id = default;
            addon.Actions = new List<ActionModel>();
            addon.Resources = new List<ResourceModel>();
            addon.Templates = new List<CardModel>();
            addon.Views = new List<CardModel>();

            // Upfront total for progress reporting — a rough estimate, not byte-exact: entries
            // skipped later by CheckIfAlreadyExists() won't call ReportProgress, so Current may
            // not always reach Total. Good enough for a UX progress bar.
            var total = (addon.Dependencies?.Count ?? 0)
                + GetByFolder(archive, "scripts/").Count()
                + GetByFolder(archive, "resources/").Count()
                + GetByFolder(archive, "actions/").Count()
                + GetByFolder(archive, "templates/").Count()
                + GetByFolder(archive, "views/").Count();
            var current = 0;
            void ReportProgress(string phase, string? message = null)
            {
                current++;
                request.OnProgress?.Invoke(new InstallAddonProgress { Phase = phase, Current = current, Total = total, Message = message });
            }

            await FindAndInstallDependencies(request, game, addon, ReportProgress);

            // Clear deps — dependencies are installed separately, not stored on the addon entity
            addon.Dependencies = null;

            var addonsFolderId = await CreateFolder(request, game, "Addons");
            var addonFolderId = await CreateFolder(request, game, addon.Name, addonsFolderId);

            await AddScripts(request, archive, addon, addonFolderId, game, ReportProgress);
            await AddResources(request, archive, addon, addonFolderId, game, ReportProgress);
            await AddActions(request, archive, addon, game, ReportProgress);
            await AddTemplates(request, archive, addon, game, ReportProgress);
            await AddViews(request, archive, addon, game, ReportProgress);

            game.Addons.Add(addon);

            // Bug fix: was using synchronous SaveChanges in an async handler
            await dbContext.SaveChangesAsync();

            addon.SetGlobalPermission();
            addon.SetPermissions(game.MasterId, Permission.All);

            return (CommandResponse.Ok, new InstallAddonCommandResponse
            {
                AddonId = addon.Id,
                AddonKey = addon.Key,
                AddonName = addon.Name,
                AddonVersion = addon.Version
            });
        }

        private async Task AddViews(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game, Action<string, string?> reportProgress)
        {
            foreach (var view in GetByFolder(archive, "views/"))
            {
                var rawDto = JsonSerializer.Deserialize<CardInstallDto>(ReadToBytes(view), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Failed to deserialize view '{view.FullName}'.");
                var dto = ResolveCardDto(rawDto, game, addon.Resources);

                var (_, res) = await mediator.Send(new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = dto,
                    IsCustomUi = true,
                    IsTemplate = false
                });

                // Bug fix: was incorrectly adding to addon.Templates instead of addon.Views
                var card = FindCreatedCard(res, game)
                    ?? throw new InvalidOperationException($"Card '{res}' not found after adding view '{dto.Name}'.");

                if (dto.GenericPermission.HasValue)
                {
                    card.ClearPermissions(Guid.Empty);
                    card.SetGlobalPermission(dto.GenericPermission.Value);
                }
                if (dto.GmPermission.HasValue)
                {
                    card.ClearPermissions(game.MasterId);
                    card.SetPermissions(game.MasterId, dto.GmPermission.Value);
                }

                addon.Views!.Add(card);

                gameEventLogger.Info("AddonInstall", $"Added view '{dto.Name}' (Key: {addon.Key + "_" + dto.Name}) to addon '{addon.Name}'.");
                reportProgress("views", $"Added view '{dto.Name}'");
            }
        }

        private async Task AddTemplates(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game, Action<string, string?> reportProgress)
        {
            foreach (var template in GetByFolder(archive, "templates/"))
            {
                var rawDto = JsonSerializer.Deserialize<CardInstallDto>(ReadToBytes(template), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Failed to deserialize template '{template.FullName}'.");
                var dto = ResolveCardDto(rawDto, game, addon.Resources);

                var (_, res) = await mediator.Send(new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = dto,
                    IsCustomUi = false,
                    IsTemplate = true
                });

                var card = FindCreatedCard(res, game)
                    ?? throw new InvalidOperationException($"Card '{res}' not found after adding template '{dto.Name}'.");

                if (dto.GenericPermission.HasValue)
                {
                    card.ClearPermissions(Guid.Empty);
                    card.SetGlobalPermission(dto.GenericPermission.Value);
                }
                if (dto.GmPermission.HasValue)
                {
                    card.ClearPermissions(game.MasterId);
                    card.SetPermissions(game.MasterId, dto.GmPermission.Value);
                }

                addon.Templates!.Add(card);

                gameEventLogger.Info("AddonInstall", $"Added template '{dto.Name}' (Key: {addon.Key + "_" + dto.Name}) to addon '{addon.Name}'.");
                reportProgress("templates", $"Added template '{dto.Name}'");
            }
        }

        private async Task AddActions(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game, Action<string, string?> reportProgress)
        {
            foreach (var action in GetByFolder(archive, "actions/"))
            {
                var dto = DeserializeAction(ReadToBytes(action), action.FullName);

                dto.Prefix = addon.Key;
                // ActionDto.IsEnabled has no explicit default, so it deserializes to false
                // unless the addon's JSON happens to say "isEnabled": true (which authors
                // have no reason to write — an addon's own actions are meant to work
                // immediately after install, same as AddonModel.IsEnabled defaults to true).
                // Without this, every addon-installed action silently starts disabled and
                // Hook.Install (and any other hook) never actually runs it.
                dto.IsEnabled = true;

                var (_, result) = await mediator.Send(new AddActionCommand
                {
                    GameId = request.GameID,
                    Player = request.Player,
                    Action = dto
                });

                var actionModel = dbContext.Actions.Find(result)
                    ?? throw new InvalidOperationException($"Action '{result}' not found after adding '{dto.Name}'.");

                if (dto.GenericPermission.HasValue)
                {
                    actionModel.ClearPermissions(Guid.Empty);
                    actionModel.SetGlobalPermission(dto.GenericPermission.Value);
                }
                if (dto.GmPermission.HasValue)
                {
                    actionModel.ClearPermissions(game.MasterId);
                    actionModel.SetPermissions(game.MasterId, dto.GmPermission.Value);
                }

                addon.Actions!.Add(actionModel);

                gameEventLogger.Info("AddonInstall", $"Added action '{dto.Name}' (Key: {addon.Key + "_" + dto.Name}) to addon '{addon.Name}'.");
                reportProgress("actions", $"Added action '{dto.Name}'");
            }
        }

        private async Task AddResources(InstallAddonCommand request, ZipArchive archive, AddonModel addon, Guid? addonFolderId, GameModel game, Action<string, string?> reportProgress)
        {
            var resourcesFolder = await CreateFolder(request, game, "Resources", addonFolderId);

            foreach (var resource in GetByFolder(archive, "resources/"))
            {
                if (!CheckIfAlreadyExists(request, addon, game, resource))
                    continue;

                var (_, resourceID) = await mediator.Send(new AddResourceCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    MimeType = resource.FullName.ToMimeType()?.GetDescriptionValue(),
                    Name = resource.Name,
                    Key = addon.Key + "_" + resource.Name,
                    ParentFolder = resourcesFolder,
                    DataRaw = ReadToBytes(resource)
                });

                if (resourceID == null || resourceID == Guid.Empty)
                    throw new InvalidOperationException($"Failed to add resource '{resource.Name}'.");

                // Bug fix: was not null-checking dbContext.Find result
                var model = dbContext.Find<ResourceModel>(resourceID.Value)
                    ?? throw new InvalidOperationException($"Resource '{resourceID}' not found in DB after adding.");

                addon.Resources!.Add(model);

                gameEventLogger.Info("AddonInstall", $"Added resource '{resource.Name}' (Key: {addon.Key + "_" + resource.Name}) to addon '{addon.Name}'.");
                reportProgress("resources", $"Added resource '{resource.Name}'");
            }
        }

        private async Task AddScripts(InstallAddonCommand request, ZipArchive archive, AddonModel addon, Guid? addonFolderId, GameModel game, Action<string, string?> reportProgress)
        {
            var scriptsFolder = await CreateFolder(request, game, "Scripts", addonFolderId);

            foreach (var script in GetByFolder(archive, "scripts/"))
            {
                if (!CheckIfAlreadyExists(request, addon, game, script))
                    continue;

                var (_, resourceID) = await mediator.Send(new AddResourceCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    MimeType = script.FullName.ToMimeType()?.GetDescriptionValue(),
                    Name = script.Name,
                    Key = addon.Key + "_" + script.Name,
                    ParentFolder = scriptsFolder,
                    DataRaw = ReadToBytes(script)
                });

                if (resourceID == null || resourceID == Guid.Empty)
                    throw new InvalidOperationException($"Failed to add script '{script.Name}'.");

                // Bug fix: was not null-checking dbContext.Find result
                var model = dbContext.Find<ResourceModel>(resourceID.Value)
                    ?? throw new InvalidOperationException($"Resource '{resourceID}' not found in DB after adding.");
                addon.Resources!.Add(model);

                gameEventLogger.Info("AddonInstall", $"Added script '{script.Name}' (Key: {addon.Key + "_" + script.Name}) to addon '{addon.Name}'.");
                reportProgress("scripts", $"Added script '{script.Name}'");
            }
        }

        // Bug fix: was calling dbContext.SaveChanges() per-resource — removed, top-level SaveChangesAsync handles it
        private bool CheckIfAlreadyExists(InstallAddonCommand request, AddonModel addon, GameModel game, ZipArchiveEntry entry)
        {
            var key = addon.Key + "_" + entry.Name;
            var existing = dbContext.Resources.FirstOrDefault(x => x.GameId == game.Id && x.Key == key);
            if (existing == null)
                return true;

            if (request.Reinstall)
            {
                dbContext.Remove(existing);
                return true;
            }

            return false;
        }

        private static IEnumerable<ZipArchiveEntry> GetByFolder(ZipArchive archive, string folder)
        {
            var folderLower = folder.ToLowerInvariant().Trim();
            return archive.Entries.Where(x =>
            {
                var name = x.FullName.ToLowerInvariant().Trim();
                if (!name.StartsWith(folderLower) || name == folderLower) return false;

                // Directory entries (x.Name is empty — the entry IS the folder marker,
                // e.g. "resources/subfolder/") and dotfile housekeeping entries
                // (.gitkeep, .DS_Store, ...) are not real payload — every addon
                // author's zip is likely to contain some of these (git/OS tooling
                // adds them automatically), and none of the downstream processing
                // (mimetype inference, JSON deserialization) is meant to handle
                // them. Filtered here, once, rather than trusting every caller of
                // GetByFolder to defend against non-payload entries individually.
                if (string.IsNullOrEmpty(x.Name) || x.Name.StartsWith('.')) return false;

                return true;
            });
        }

        private async Task<Guid?> CreateFolder(InstallAddonCommand request, GameModel game, string name, Guid? parentFolderId = null)
        {
            var resourceTree = typeof(ResourceModel).Name;
            var existing = dbContext.TreeEntries.FirstOrDefault(x =>
                x.Game.Id == game.Id
                && x.Name == name
                && x.EntryType == resourceTree
                && (parentFolderId == null ? x.Parent == null : x.Parent != null && x.Parent.Id == parentFolderId));

            if (existing != null)
                return existing.Id;

            var result = await mediator.Send(new AddTreeEntryCommand
            {
                GameId = request.GameID,
                Player = request.Player,
                TreeEntryDto = new TreeEntryDto
                {
                    Name = name,
                    EntryType = typeof(ResourceModel).Name,
                    ParentId = parentFolderId,
                    IsFolder = true,
                    AutoConnect = true,
                }
            });

            return result.Item2.FirstOrDefault(x => x.Name == name)?.Id;
        }

        private async Task FindAndInstallDependencies(InstallAddonCommand request, GameModel game, AddonModel addon, Action<string, string?> reportProgress)
        {
            if (addon.Dependencies == null || addon.Dependencies.Count == 0)
                return;

            foreach (var dependency in addon.Dependencies)
            {
                gameEventLogger.Info("AddonInstall", $"Checking dependency '{dependency.Key}' (v{dependency.Version}) for addon '{addon.Name}'.");

                // Bug fix: was matching by name which can differ — now matches by key
                var alreadyInstalled = game.Addons.Any(x => x.Key == dependency.Key && CompareVersions(x, dependency));
                if (alreadyInstalled)
                {
                    gameEventLogger.Info("AddonInstall", $"Dependency '{dependency.Key}' (v{dependency.Version}) is already installed for game '{game.Name}'.");
                    reportProgress("dependencies", $"Dependency '{dependency.Key}' already installed");
                    continue;
                }

                if (request.AutoInstallDeps == false)
                    throw new InvalidOperationException($"Dependency '{dependency.Key}' (v{dependency.Version}) is not installed and auto-install is disabled.");

                gameEventLogger.Info("AddonInstall", $"Auto-installing dependency '{dependency.Key}' (v{dependency.Version}) for addon '{addon.Name}'.");

                // Bug fix: was passing version arg that no longer exists on the interface
                var depFile = await addonFromUriProvider.GetAddonByKey(dependency.Key);
                if (depFile == null)
                    throw new InvalidOperationException($"Failed to fetch addon file for dependency '{dependency.Key}'.");

                gameEventLogger.Info("AddonInstall", $"Fetched addon file for dependency '{dependency.Key}'.");

                await mediator.Send(new InstallAddonCommand
                {
                    AddonFile = depFile,
                    AutoInstallDeps = true,
                    GameID = request.GameID,
                    Player = request.Player
                });

                reportProgress("dependencies", $"Installed dependency '{dependency.Key}'");
            }
        }

        // Bug fix: null version on requirement now means "any version is acceptable"
        private static bool CompareVersions(AddonModel installed, AddonModel required)
            => required.Version == null || installed.Version == required.Version;

        /// <summary>
        /// Deserializes an action JSON file, accepting <c>content</c> as either a
        /// pre-serialized JSON string or a raw JSON array — the latter is serialized
        /// back to a string so addon authors can write human-readable step arrays.
        /// </summary>
        private static ActionDto DeserializeAction(byte[] bytes, string fileName)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(bytes);

            // Fast path: content is already a string (or absent) — deserialize directly.
            if (!doc.RootElement.TryGetProperty("content", out var contentEl)
                || contentEl.ValueKind == JsonValueKind.String)
            {
                return JsonSerializer.Deserialize<ActionDto>(bytes, options)
                    ?? throw new InvalidOperationException($"Failed to deserialize action '{fileName}'.");
            }

            // content is a JSON array — rewrite it as a serialized string so it fits
            // the ActionDto.Content field (which the database stores as a JSON string).
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals("content", StringComparison.OrdinalIgnoreCase))
                    writer.WriteString(prop.Name, prop.Value.GetRawText());
                else
                    prop.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();

            return JsonSerializer.Deserialize<ActionDto>(ms.ToArray(), options)
                ?? throw new InvalidOperationException($"Failed to deserialize action '{fileName}'.");
        }

        private static byte[] ReadToBytes(ZipArchiveEntry entry)
        {
            using var memoryStream = new MemoryStream();
            using var stream = entry.Open();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Resolves a resource reference that may be either a GUID string or a resource key:
        /// first among this addon's own new resources, then among the game's resources (by id
        /// only — the resources themselves aren't loaded).
        /// </summary>
        private CardDto ResolveCardDto(CardInstallDto raw, GameModel game, IEnumerable<ResourceModel> addonResources)
        {
            Guid? ResolveResourceRef(string? value)
            {
                if (value == null) return null;
                if (Guid.TryParse(value, out var guid)) return guid;
                return addonResources.FirstOrDefault(r => r.Key == value)?.Id
                    ?? dbContext.Resources.Where(r => r.GameId == game.Id && r.Key == value).Select(r => (Guid?)r.Id).FirstOrDefault();
            }

            return new CardDto
            {
                Id                 = raw.Id,
                Name               = raw.Name,
                Description        = raw.Description,
                Key                = raw.Key,
                FirstOpen          = raw.FirstOpen,
                TemplateId         = raw.TemplateId,
                Owner              = raw.Owner,
                MainResource       = ResolveResourceRef(raw.MainResource),
                AdditionalResources = raw.AdditionalResources?
                    .Select(r => ResolveResourceRef(r))
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList(),
                Permission         = raw.Permission,
                GenericPermission  = raw.GenericPermission,
                GmPermission       = raw.GmPermission,
                Properties         = raw.Properties ?? Enumerable.Empty<PropertyDTO>(),
            };
        }

        /// <summary>A card AddCardCommand just created in this game (already tracked, so no query).</summary>
        private CardModel? FindCreatedCard(Guid id, GameModel game)
        {
            var card = dbContext.Cards.Find(id);
            return card?.GameId == game.Id ? card : null;
        }

        /// <summary>
        /// Intermediate DTO used when deserializing card JSON from addon archives.
        /// MainResource and AdditionalResources accept either a GUID string or a
        /// resource key — resolved to GUIDs by ResolveCardDto before use.
        /// </summary>
        private sealed class CardInstallDto
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? Key { get; set; }
            public bool? FirstOpen { get; set; }
            public Guid? TemplateId { get; set; }
            public Guid? Owner { get; set; }
            public string? MainResource { get; set; }
            public List<string>? AdditionalResources { get; set; }
            public Permission? Permission { get; set; }
            public Permission? GenericPermission { get; set; }
            public Permission? GmPermission { get; set; }
            public IEnumerable<PropertyDTO>? Properties { get; set; }
        }
    }
}
