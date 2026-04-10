using AutoMapper;
using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
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
    internal class InstallAddonCommandHandler : HandlerBase<InstallAddonCommand, (CommandResponse, Guid)>
    {
        private readonly IDbContext dbContext;
        private readonly IMapper mapper;
        private readonly IAddonRepositoryService addonFromUriProvider;
        private readonly IMediator mediator;

        public InstallAddonCommandHandler(IDbContext dbContext, IMapper mapper, IAddonRepositoryService addonFromUriProvider, IMediator mediator) : base(dbContext, mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
            this.addonFromUriProvider = addonFromUriProvider;
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid)> Handle(InstallAddonCommand request, CancellationToken cancellationToken)
        {
            var game = dbContext.Games
                .Include(x => x.Addons)
                .Include(x => x.Resources)
                .Include(x => x.TreeEntries)
                .Include(x => x.Actions)
                .Include(x => x.Cards)
                .Include(x => x.Properties)
                .FirstOrDefault(x => x.Id == request.GameID);

            // Bug fix: was crashing with NullReferenceException when game not found
            if (game == null)
                return (CommandResponse.NoResource, Guid.Empty);

            if (!game.HasPermission(request.Player.Id ?? default, Permission.Edit))
                return (CommandResponse.NoPermission, Guid.Empty);

            // Bug fix: both null would silently fall through to ZipArchive(null) crash
            if (request.AddonFile == null && request.AddonSourceKey == null)
                throw new ArgumentException("Either AddonFile or AddonSourceKey must be provided.");

            if (request.AddonFile == null)
                request.AddonFile = await addonFromUriProvider.GetAddonByKey(request.AddonSourceKey!);

            using var archive = new ZipArchive(new MemoryStream(request.AddonFile));

            // Bug fix: GetEntry can return null — was crashing in ReadToBytes
            var infoEntry = archive.Entries.FirstOrDefault(x => x.Name.ToLower().Trim() == "info.json")
                ?? throw new InvalidOperationException("Addon archive is missing 'info.json'. Is this a valid addon file?");

            var addon = JsonSerializer.Deserialize<AddonModel>(ReadToBytes(infoEntry), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Cannot deserialize 'info.json'. Are you sure this is a valid addon file?");

            if (addon.Key == null)
                throw new InvalidOperationException("Addon 'info.json' is missing the required 'key' field.");

            // Reset navigation collections so EF doesn't try to re-attach stale entries
            addon.Id = default;
            addon.Actions = new List<ActionModel>();
            addon.Resources = new List<ResourceModel>();
            addon.Templates = new List<CardModel>();
            addon.Views = new List<CardModel>();

            await FindAndInstallDependencies(request, game, addon);

            // Clear deps — dependencies are installed separately, not stored on the addon entity
            addon.Dependencies = null;

            var addonsFolderId = await CreateFolder(request, game, "Addons");
            var addonFolderId = await CreateFolder(request, game, addon.Name, addonsFolderId);

            await AddScripts(request, archive, addon, addonFolderId, game);
            await AddResources(request, archive, addon, addonFolderId, game);
            await AddActions(request, archive, addon, game);
            await AddTemplates(request, archive, addon, game);
            await AddViews(request, archive, addon, game);

            game.Addons.Add(addon);

            // Bug fix: was using synchronous SaveChanges in an async handler
            await dbContext.SaveChangesAsync();

            addon.SetGlobalPermission();
            addon.SetPermissions(game.MasterId, Permission.All);

            return (CommandResponse.Ok, addon.Id);
        }

        private async Task AddViews(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game)
        {
            foreach (var view in GetByFolder(archive, "views/"))
            {
                var dto = JsonSerializer.Deserialize<CardDto>(ReadToBytes(view), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Failed to deserialize view '{view.FullName}'.");

                var (_, res) = await mediator.Send(new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = dto,
                    IsCustomUi = true,
                    IsTemplate = false
                });

                // Bug fix: was incorrectly adding to addon.Templates instead of addon.Views
                var card = game.Cards.FirstOrDefault(x => x.Id == res)
                    ?? throw new InvalidOperationException($"Card '{res}' not found after adding view '{dto.Name}'.");
                addon.Views!.Add(card);
            }
        }

        private async Task AddTemplates(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game)
        {
            foreach (var template in GetByFolder(archive, "templates/"))
            {
                var dto = JsonSerializer.Deserialize<CardDto>(ReadToBytes(template), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Failed to deserialize template '{template.FullName}'.");

                var (_, res) = await mediator.Send(new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = dto,
                    IsCustomUi = false,
                    IsTemplate = true
                });

                var card = game.Cards.FirstOrDefault(x => x.Id == res)
                    ?? throw new InvalidOperationException($"Card '{res}' not found after adding template '{dto.Name}'.");
                addon.Templates!.Add(card);
            }
        }

        private async Task AddActions(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game)
        {
            foreach (var action in GetByFolder(archive, "actions/"))
            {
                var dto = JsonSerializer.Deserialize<ActionDto>(ReadToBytes(action), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Failed to deserialize action '{action.FullName}'.");

                dto.Prefix = addon.Key;

                var (_, result) = await mediator.Send(new AddActionCommand
                {
                    GameId = request.GameID,
                    Player = request.Player,
                    Action = dto
                });

                var actionModel = game.Actions.FirstOrDefault(x => x.Id == result)
                    ?? throw new InvalidOperationException($"Action '{result}' not found after adding '{dto.Name}'.");
                addon.Actions!.Add(actionModel);
            }
        }

        private async Task AddResources(InstallAddonCommand request, ZipArchive archive, AddonModel addon, Guid? addonFolderId, GameModel game)
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
            }
        }

        private async Task AddScripts(InstallAddonCommand request, ZipArchive archive, AddonModel addon, Guid? addonFolderId, GameModel game)
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
            }
        }

        // Bug fix: was calling dbContext.SaveChanges() per-resource — removed, top-level SaveChangesAsync handles it
        private bool CheckIfAlreadyExists(InstallAddonCommand request, AddonModel addon, GameModel game, ZipArchiveEntry entry)
        {
            var existing = game.Resources.FirstOrDefault(x => x.Key == addon.Key + "_" + entry.Name);
            if (existing == null)
                return true;

            if (request.Reinstall)
            {
                game.Resources.Remove(existing);
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
                return name.StartsWith(folderLower) && name != folderLower;
            });
        }

        private async Task<Guid?> CreateFolder(InstallAddonCommand request, GameModel game, string name, Guid? parentFolderId = null)
        {
            var existing = game.TreeEntries.FirstOrDefault(x =>
                x.Name == name
                && x.EntryType == typeof(ResourceModel).Name
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

        private async Task FindAndInstallDependencies(InstallAddonCommand request, GameModel game, AddonModel addon)
        {
            if (addon.Dependencies == null || addon.Dependencies.Count == 0)
                return;

            foreach (var dependency in addon.Dependencies)
            {
                // Bug fix: was matching by name which can differ — now matches by key
                var alreadyInstalled = game.Addons.Any(x => x.Key == dependency.Key && CompareVersions(x, dependency));
                if (alreadyInstalled)
                    continue;

                if (request.AutoInstallDeps == false)
                    throw new InvalidOperationException($"Dependency '{dependency.Key}' (v{dependency.Version}) is not installed and auto-install is disabled.");

                // Bug fix: was passing version arg that no longer exists on the interface
                var depFile = await addonFromUriProvider.GetAddonByKey(dependency.Key);

                var (response, _) = await mediator.Send(new InstallAddonCommand
                {
                    AddonFile = depFile,
                    AutoInstallDeps = true,
                    GameID = request.GameID,
                    Player = request.Player
                });

                if (response != CommandResponse.Ok)
                    throw new InvalidOperationException($"Failed to install dependency '{dependency.Key}'. Response: {response}");
            }
        }

        // Bug fix: null version on requirement now means "any version is acceptable"
        private static bool CompareVersions(AddonModel installed, AddonModel required)
            => required.Version == null || installed.Version == required.Version;

        private static byte[] ReadToBytes(ZipArchiveEntry entry)
        {
            using var memoryStream = new MemoryStream();
            using var stream = entry.Open();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
