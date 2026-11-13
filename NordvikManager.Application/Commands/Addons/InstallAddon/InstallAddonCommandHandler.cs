using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.Commands.Card;
using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Addons.UninstallAddon;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Interfaces;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

namespace NordvikManager.Application.Commands.Addons.InstallAddon
{
    internal class InstallAddonCommandHandler : HandlerBase<InstallAddonCommand, (CommandResponse, InstallAddonCommandResponse)>
    {
        private readonly IDbContext dbContext;
        private readonly IMapper mapper;
        private readonly IAddonRepositoryService addonFromUriProvider;
        private readonly IMediator mediator;
        private readonly IGameEventLogger gameEventLogger;
        private readonly IBuiltInAddons builtInAddons;

        public InstallAddonCommandHandler(IDbContext dbContext, IMapper mapper, IAddonRepositoryService addonFromUriProvider, IMediator mediator, IGameEventLogger gameEventLogger, IBuiltInAddons builtInAddons) : base(dbContext, mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
            this.addonFromUriProvider = addonFromUriProvider;
            this.mediator = mediator;
            this.gameEventLogger = gameEventLogger;
            this.builtInAddons = builtInAddons;
        }

        public override async Task<(CommandResponse, InstallAddonCommandResponse)> Handle(InstallAddonCommand request, CancellationToken cancellationToken)
        {
            // Only the addons list. Resources, tree entries, cards and actions used to be loaded
            // here too (all of them, resource file bytes included) — on a game with a big
            // addon that was tens of thousands of rows per install, and once failed outright
            // with SQLite "disk full". Everything below looks up just what it needs instead.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            gameEventLogger.Info("AddonInstall", $"[trace] handler start: sourceKey={request.AddonSourceKey} file={request.AddonFileName} reinstall={request.Reinstall}");
            var game = dbContext.Games
                .Include(x => x.Addons)
                .FirstOrDefault(x => x.Id == request.GameID);
            gameEventLogger.Info("AddonInstall", $"[trace] game loaded ({watch.ElapsedMilliseconds} ms), {game?.Addons?.Count ?? 0} addon(s) installed");

            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            // Bug fix: both null would silently fall through to ZipArchive(null) crash
            if (request.AddonFile == null && request.AddonSourceKey == null)
                throw new ArgumentException("Either AddonFile or AddonSourceKey must be provided.");

            gameEventLogger.Info("AddonInstall", $"Installing addon for game '{game.Name}' (ID: {game.Id}) by player '{request.Player.Name}' (ID: {request.Player.Id}).");
            
            if (request.AddonFile == null)
            {
                gameEventLogger.Info("AddonInstall", $"[trace] downloading '{request.AddonSourceKey}'…");
                request.AddonFile = await addonFromUriProvider.GetAddonByKey(request.AddonSourceKey!);
                gameEventLogger.Info("AddonInstall", $"[trace] downloaded {request.AddonFile?.Length ?? 0} bytes ({watch.ElapsedMilliseconds} ms)");
                gameEventLogger.Info("AddonInstall", $"Fetched addon file from source key '{request.AddonSourceKey}");
            }

            using var archive = new ZipArchive(new MemoryStream(request.AddonFile));
            var addon = ReadInfo(archive);

            // Without this, installing an addon that's already present for this game (e.g. it
            // was pulled in as another addon's dependency, then also selected directly — see
            // FindAndInstallDependencies, which only guards its OWN recursive calls, not a
            // top-level request like this one) silently created a second AddonModel row and
            // re-ran AddActions/AddTemplates/AddViews with no existence check of their own,
            // duplicating every action and card. Two AddonModel rows for the same Key each got
            // their own Hook.Install fired later, and since CallHookAsync queries actions
            // game-wide rather than per-addon, that ran every duplicated action multiple times.
            var existingAddon = game.Addons.FirstOrDefault(x => x.Key == addon.Key);
            gameEventLogger.Info("AddonInstall", $"[trace] archive read: key={addon.Key} version={addon.Version}, already installed={existingAddon != null} ({watch.ElapsedMilliseconds} ms)");
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

            }

            // Everything this addon needs, found and checked before the game is touched: a
            // dependency that can't be fetched, is too old or depends back on the addon fails
            // here, while an update still has the old version installed.
            var dependencySteps = await PlanDependencies(request, game, addon);

            // The old version's files by key, so cards pointing at them can follow the new ones.
            Dictionary<Guid, string>? replacedFiles = null;
            if (existingAddon != null)
            {
                replacedFiles = dbContext.Addons
                    .Where(a => a.Id == existingAddon.Id)
                    .SelectMany(a => a.Resources!)
                    .Where(r => r.Key != null)
                    .Select(r => new { r.Id, r.Key })
                    .ToDictionary(r => r.Id, r => r.Key!);

                // Cards made from its templates remember the templates' keys, so they link to
                // the new version's templates with the same keys (TemplateLinks).
                TemplateLinks.RememberKeys(dbContext, game.Id, dbContext.Addons
                    .Where(a => a.Id == existingAddon.Id)
                    .SelectMany(a => a.Templates!)
                    .ToList());

                gameEventLogger.Info("AddonInstall", $"Addon '{addon.Key}' is already installed for game '{game.Name}' — removing the existing install before reinstalling.");
                await mediator.Send(new UninstallAddonCommand
                {
                    Player = request.Player,
                    GameID = request.GameID,
                    AddonId = existingAddon.Id,
                    ForReinstall = true,
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
            var total = dependencySteps.Count
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

            gameEventLogger.Info("AddonInstall", $"[trace] dependencies to install: {dependencySteps.Count}, items to add ≈ {total}");
            var installedDependencies = new List<InstallAddonCommandResponse>();
            foreach (var step in dependencySteps)
            {
                gameEventLogger.Info("AddonInstall", $"{(step.Replaces ? "Updating" : "Installing")} dependency '{step.Key}' (v{step.Version}) for addon '{addon.Name}'.");
                var (_, installedDependency) = await mediator.Send(new InstallAddonCommand
                {
                    AddonFile = step.File,
                    AutoInstallDeps = true,
                    Reinstall = step.Replaces,
                    GameID = request.GameID,
                    Player = request.Player,
                });
                if (installedDependency != null)
                    installedDependencies.Add(installedDependency);
                ReportProgress("dependencies", $"{(step.Replaces ? "Updated" : "Installed")} dependency '{step.Key}'");
            }
            gameEventLogger.Info("AddonInstall", $"[trace] dependencies done ({watch.ElapsedMilliseconds} ms)");

            var addonsFolderId = await CreateFolder(request, game, "Addons");
            var addonFolderId = await CreateFolder(request, game, addon.Name, addonsFolderId);

            await AddScripts(request, archive, addon, addonFolderId, game, ReportProgress);
            await AddResources(request, archive, addon, addonFolderId, game, ReportProgress);
            await AddActions(request, archive, addon, game, ReportProgress);
            await AddTemplates(request, archive, addon, game, ReportProgress);
            await AddViews(request, archive, addon, game, ReportProgress);
            gameEventLogger.Info("AddonInstall", $"[trace] content added ({watch.ElapsedMilliseconds} ms), saving…");

            game.Addons.Add(addon);

            // Bug fix: was using synchronous SaveChanges in an async handler
            await dbContext.SaveChangesAsync();

            if (replacedFiles is { Count: > 0 })
                await RepointCards(game, replacedFiles, addon.Resources!);
            gameEventLogger.Info("AddonInstall", $"[trace] saved, handler done ({watch.ElapsedMilliseconds} ms)");

            addon.SetGlobalPermission();
            addon.SetPermissions(game.MasterId, Permission.All);

            return (CommandResponse.Ok, new InstallAddonCommandResponse
            {
                AddonId = addon.Id,
                AddonKey = addon.Key,
                AddonName = addon.Name,
                AddonVersion = addon.Version,
                InstalledDependencies = installedDependencies,
            });
        }

        /// <summary>
        /// After an update: the game's cards that pointed at the old version's files (another
        /// addon's cards, cards made from its templates) point at the new ones with the same key.
        /// A file the new version no longer has is left as it was.
        /// </summary>
        private async Task RepointCards(GameModel game, Dictionary<Guid, string> replacedFiles, IEnumerable<ResourceModel> newFiles)
        {
            var newIdByKey = newFiles.Where(r => r.Key != null).GroupBy(r => r.Key!).ToDictionary(g => g.Key, g => g.First().Id);
            var moves = replacedFiles
                .Where(old => newIdByKey.ContainsKey(old.Value))
                .ToDictionary(old => old.Key, old => newIdByKey[old.Value]);
            if (moves.Count == 0)
                return;

            var oldIds = moves.Keys.ToList();
            var cards = dbContext.Cards
                .Where(c => c.GameId == game.Id
                    && ((c.MainResource != null && oldIds.Contains(c.MainResource.Value))
                        || (c.AdditionalResources != null && c.AdditionalResources != "" && c.AdditionalResources != "[]")))
                .ToList();

            var changed = 0;
            foreach (var card in cards)
            {
                var touched = false;
                if (card.MainResource is Guid main && moves.TryGetValue(main, out var newMain))
                {
                    card.MainResource = newMain;
                    touched = true;
                }

                var additional = ParseIds(card.AdditionalResources);
                if (additional != null && additional.Any(moves.ContainsKey))
                {
                    card.AdditionalResources = JsonSerializer.Serialize(additional.Select(id => moves.TryGetValue(id, out var moved) ? moved : id));
                    touched = true;
                }

                if (touched)
                    changed++;
            }

            if (changed > 0)
            {
                await dbContext.SaveChangesAsync();
                gameEventLogger.Info("AddonInstall", $"Pointed {changed} card(s) at the new version's files.");
            }

            static List<Guid>? ParseIds(string? json)
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                try { return JsonSerializer.Deserialize<List<Guid>>(json); }
                catch (JsonException) { return null; }
            }
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

                Guid result;
                try
                {
                    (_, result) = await mediator.Send(new AddActionCommand
                    {
                        GameId = request.GameID,
                        Player = request.Player,
                        Action = dto
                    });
                }
                catch (WrongArgumentsException e)
                {
                    // E.g. a Chat Command action without its commands: say which file.
                    throw WrongArgumentsException.Because($"{action.FullName}: {e.Message}");
                }

                var actionModel = dbContext.Actions.Find(result)
                    ?? throw new InvalidOperationException($"Action '{result}' not found after adding '{dto.Name}'.");

                if (dto.GenericPermission.HasValue)
                {
                    actionModel.ClearPermissions(Guid.Empty);
                    actionModel.SetGlobalPermission(dto.GenericPermission.Value);
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
                    SkipQuota = true, // addon files: not uploads
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
                    SkipQuota = true, // addon files: not uploads
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

            // A player's upload that took the key first is never adopted as the addon's file.
            if (request.Reinstall || !game.IsRunBy(existing.PlayerId))
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

        /// <summary>A dependency to install (or, when an older version is installed, to update), with its package.</summary>
        private sealed record DependencyStep(string Key, string? Version, byte[] File, bool Replaces);

        /// <summary>
        /// Walks the addon's dependencies, theirs, and so on: each one missing or older than
        /// required is downloaded and its info.json checked. Returns them deepest first, each
        /// once. Throws, before anything is installed, on a circle (naming it), a dependency
        /// the registry can't give, or one whose newest release is still too old.
        /// </summary>
        private async Task<List<DependencyStep>> PlanDependencies(InstallAddonCommand request, GameModel game, AddonModel addon)
        {
            var plan = new List<DependencyStep>();
            await Walk(addon, new List<string> { addon.Key });
            return plan;

            async Task Walk(AddonModel current, List<string> chain)
            {
                foreach (var dependency in current.Dependencies ?? new List<AddonDependency>())
                {
                    if (string.IsNullOrWhiteSpace(dependency.Key))
                        throw new InvalidOperationException($"'{current.Key}' lists a dependency without a key.");

                    if (chain.Contains(dependency.Key))
                        throw new InvalidOperationException($"These addons depend on each other in a circle: {string.Join(" → ", chain.Append(dependency.Key))}.");

                    var planned = plan.FirstOrDefault(p => p.Key == dependency.Key);
                    if (planned != null)
                    {
                        if (!AddonVersion.Satisfies(planned.Version, dependency.Version))
                            throw TooOld(current, dependency, planned.Version);
                        continue;
                    }

                    var installed = game.Addons.FirstOrDefault(x => x.Key == dependency.Key);
                    if (installed != null && AddonVersion.Satisfies(installed.Version, dependency.Version))
                    {
                        gameEventLogger.Info("AddonInstall", $"Dependency '{dependency.Key}' (v{installed.Version}) of '{current.Key}' is already installed.");
                        continue;
                    }

                    if (request.AutoInstallDeps == false)
                        throw new InvalidOperationException($"Dependency '{dependency.Key}' (v{dependency.Version}) is not installed and auto-install is disabled.");

                    // The server's own copy of a built-in addon (Basics isn't in the registry),
                    // when it's new enough; otherwise the registry's.
                    var builtIn = builtInAddons.GetAll().FirstOrDefault(b => b.Key == dependency.Key && AddonVersion.Satisfies(b.Version, dependency.Version));
                    var file = builtIn?.Data
                        ?? await addonFromUriProvider.GetAddonByKey(dependency.Key)
                        ?? throw new InvalidOperationException($"Failed to fetch addon file for dependency '{dependency.Key}'.");

                    AddonModel info;
                    using (var archive = new ZipArchive(new MemoryStream(file)))
                        info = ReadInfo(archive);

                    if (info.Key != dependency.Key)
                        throw new InvalidOperationException($"The registry's package for '{dependency.Key}' holds the addon '{info.Key}'.");
                    if (!AddonVersion.Satisfies(info.Version, dependency.Version))
                        throw TooOld(current, dependency, info.Version);

                    // Its own dependencies go first.
                    await Walk(info, chain.Append(dependency.Key).ToList());
                    plan.Add(new DependencyStep(dependency.Key, info.Version, file, Replaces: installed != null));
                }
            }

            static InvalidOperationException TooOld(AddonModel addon, AddonDependency dependency, string? available)
                => new($"'{addon.Name ?? addon.Key}' needs '{dependency.Key}' {dependency.Version} or newer, but the registry has {available ?? "no version"}.");
        }

        /// <summary>The addon described by an archive's info.json.</summary>
        private static AddonModel ReadInfo(ZipArchive archive)
        {
            var infoEntry = archive.Entries.FirstOrDefault(x => x.Name.ToLower().Trim() == "info.json")
                ?? throw new InvalidOperationException("Addon archive is missing 'info.json'. Is this a valid addon file?");

            var addon = JsonSerializer.Deserialize<AddonModel>(ReadToBytes(infoEntry), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Cannot deserialize 'info.json'. Are you sure this is a valid addon file?");

            if (addon.Key == null)
                throw new InvalidOperationException("Addon 'info.json' is missing the required 'key' field.");

            return addon;
        }

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
                var own = addonResources.FirstOrDefault(r => r.Key == value)?.Id;
                if (own != null) return own;
                // Another addon's or the GM's file; never a player's upload under that key.
                var other = dbContext.Resources.Where(r => r.GameId == game.Id && r.Key == value)
                    .Select(r => new { r.Id, r.PlayerId }).FirstOrDefault();
                return other != null && game.IsRunBy(other.PlayerId) ? other.Id : null;
            }

            return new CardDto
            {
                Id                 = raw.Id,
                Name               = raw.Name,
                Description        = raw.Description,
                Key                = raw.Key,
                FirstOpen          = raw.FirstOpen,
                IsHidden           = raw.Hidden,
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
            // "hidden": true keeps a template out of template lists (still usable by id/name).
            public bool? Hidden { get; set; }
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
