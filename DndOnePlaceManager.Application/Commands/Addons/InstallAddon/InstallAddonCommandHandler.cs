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
            //Check if player can install addon
            var game = dbContext.Games
                .Include(x => x.Addons)
                .Include(x => x.Resources)
                .Include(x => x.TreeEntries)
                .Include(x => x.Actions)
                .Include(x => x.Cards)
                .Include(x => x.Properties)
                .FirstOrDefault(x => x.Id == request.GameID);

            if (!game.HasPermission(request.Player.Id ?? default, Permission.Edit))
            {
                return (CommandResponse.NoPermission, Guid.Empty);
            }

            if (request.AddonFile == null && request.AddonSourceKey != null)
            {
                request.AddonFile = await addonFromUriProvider.GetAddonByKey(request.AddonSourceKey);
                if (request.AddonFile == null)
                {
                    throw new Exception("No addon found in provided repositories. are you sure you provided proper key?");
                }
            }

            //if( request.AddonFile != null && game.Properties?.FirstOrDefault(x=>x.Name == "")?.Value?.ToLower() != "true") //We might want some helper methods to convert from str
            //{
            //    //await addonFromUriProvider.CheckFileSHA(request.AddonFile);
            //}

            using ZipArchive archive = new ZipArchive(new MemoryStream(request.AddonFile));
            var infoEntry = archive.GetEntry("info.json");
            var info = ReadToBytes(infoEntry);

            AddonModel? addon = JsonSerializer.Deserialize<AddonModel>(info, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (addon == null)
            {
                throw new Exception("Cannot load addon. Are you sure this is addon file?");
            }

            if (addon.Key == null)
            {
                throw new Exception("Missing key value. key value is required");
            }

            //if(game.Addons.Any(x=>x.Key == addon.Key) && !request.Reinstall)
            //{

            //}

            addon.Id = default;
            addon.Actions = new List<ActionModel>();
            addon.Resources = new List<ResourceModel>();
            addon.Templates = new List<CardModel>();
            addon.Views = new List<CardModel>();

            await FindAndInstallDependencies(request, game, addon);

            //clear deps
            addon.Dependencies = null;

            //begin installation

            //Create folder in tree directory for the addon

            var addonsFolderId = await CreateFolder(request, game, "Addons");
            var addonFolderId = await CreateFolder(request, game, addon.Name, addonsFolderId);

            await AddScripts(request, archive, addon, addonFolderId, game);

            await AddResources(request, archive, addon, addonFolderId, game);

            await AddActions(request, archive, addon, game);

            //Add all templates
            await AddTemplates(request, archive, addon, game);

            //Add all views
            await AddViews(request, archive, addon, game);

            //Add addon
            game.Addons.Add(addon);
            dbContext.SaveChanges();

            addon.SetGlobalPermission();
            addon.SetPermissions(game.MasterId, Permission.All);

            return (CommandResponse.Ok, addon.Id);
        }

        private async Task AddViews(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game)
        {
            var views = GetByFolder(archive, "views/");
            foreach (var view in views)
            {
                var viewBytes = ReadToBytes(view);
                var deserializedDto = JsonSerializer.Deserialize<CardDto>(viewBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                AddCardCommand addCardCommand = new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = deserializedDto,
                    IsCustomUi = true,
                    IsTemplate = false
                };

                var (resp, res) = await mediator.Send(addCardCommand);

                addon.Templates?.Add(game.Cards.First(x => x.Id == res));
            }
        }

        private async Task AddTemplates(InstallAddonCommand request, ZipArchive archive, AddonModel addon, GameModel game)
        {
            var templates = GetByFolder(archive, "templates/");
            foreach (var template in templates)
            {
                var templateBytes = ReadToBytes(template);
                var deserializedDto = JsonSerializer.Deserialize<CardDto>(templateBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                AddCardCommand addCardCommand = new AddCardCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Dto = deserializedDto,
                    IsCustomUi = false,
                    IsTemplate = true
                };

                var (resp, res) = await mediator.Send(addCardCommand);

                addon.Templates?.Add(game.Cards.First(x=>x.Id == res));
            }
        }

        private async Task AddActions(InstallAddonCommand request, ZipArchive archive, AddonModel? addon, GameModel game)
        {
            var actions = GetByFolder(archive, "actions/");

            foreach (var action in actions)
            {
                var actionBytes = ReadToBytes(action);
                var deserializedDto = JsonSerializer.Deserialize<ActionDto>(actionBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                deserializedDto.Prefix = addon.Key;

                AddActionCommand addActionCommand = new AddActionCommand
                {
                    GameId = request.GameID,
                    Player = request.Player,
                    Action = deserializedDto
                };

                var (resp ,result) = await mediator.Send(addActionCommand);
                addon.Actions?.Add(game.Actions.First(x=>x.Id == result));
            }
        }

        private async Task AddResources(InstallAddonCommand request, ZipArchive archive, AddonModel? addon, Guid? addonFolderId, GameModel game)
        {
            var resources = GetByFolder(archive, "resources/");

            var resourcesFolder = await CreateFolder(request, game, "Resources", addonFolderId);

            foreach (var resource in resources)
            {
                if (!CheckIfAlreadyExists(request, addon, game, resource))
                {
                    continue;
                }

                var resourceBytes = ReadToBytes(resource);

                //Get mime type based on file extension
                var mimeType = resource.FullName.ToMimeType();
                AddResourceCommand addResourceCommand = new AddResourceCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    MimeType = mimeType.GetDescriptionValue(),//? to fix
                    Name = resource.Name,
                    Key = addon.Key + "_" + resource.Name,
                    ParentFolder = resourcesFolder
                };
                addResourceCommand.DataRaw = resourceBytes;

                var (response, resourceID) = await mediator.Send(addResourceCommand);

                if (resourceID == Guid.Empty)
                {
                    throw new Exception("Cannot add resource" + resourceID);
                }

                addon.Resources.Add(dbContext.Find<ResourceModel>(resourceID.Value));
            }
        }

        private async Task AddScripts(InstallAddonCommand request, ZipArchive archive, AddonModel? addon, Guid? addonFolderId, GameModel game)
        {
            var clientScripts = GetByFolder(archive, "scripts/");

            var scriptsFolder = await CreateFolder(request, game, "Scripts", addonFolderId);

            //Add all scripts
            foreach (var script in clientScripts)
            {
                if (!CheckIfAlreadyExists(request, addon, game, script))
                {
                    continue;
                }

                var scriptBytes = ReadToBytes(script);

                //Get mime type based on file extension
                var mimeType = script.FullName.ToMimeType();
                AddResourceCommand addResourceCommand = new AddResourceCommand
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    MimeType = mimeType.GetDescriptionValue(),//? to fix
                    Name = script.Name,
                    Key = addon.Key + "_" + script.Name,
                    ParentFolder = scriptsFolder
                };
                addResourceCommand.DataRaw = scriptBytes;

                var (response, resourceID) = await mediator.Send(addResourceCommand);
                if (resourceID == Guid.Empty)
                {
                    throw new Exception("Cannot add script" + resourceID);
                }

                addon.Resources ??= new List<ResourceModel>();

                addon.Resources.Add(dbContext.Find<ResourceModel>(resourceID.Value));
            }
        }

        private bool CheckIfAlreadyExists(InstallAddonCommand request, AddonModel? addon, GameModel game, ZipArchiveEntry script)
        {
            var resource = game.Resources.FirstOrDefault(x => x.Key == addon.Key + "_" + script.Name);
            if (resource != null)
            {
                if (request.Reinstall)
                {
                    game.Resources.Remove(resource);
                    dbContext.SaveChanges();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<ZipArchiveEntry> GetByFolder(ZipArchive archive, string folder)
        {
            return archive.Entries.Where(x =>
            x.FullName.ToLower().Trim().StartsWith(folder.ToLower().Trim())
            && x.FullName.ToLower().Trim() != folder.ToLower().Trim());
        }

        private async Task<Guid?> CreateFolder(InstallAddonCommand request, GameModel game, string name, Guid? folder = null)
        {
            var existingAddon = game.TreeEntries.FirstOrDefault(x => x.Name == name && ((x.Parent != null && x.Parent?.Id == folder) || (x.Parent == null && folder == null)) && x.EntryType == typeof(ResourceModel).Name);

            if (existingAddon != null)
            {
                return existingAddon.Id;
            }

            var addonFolder = new TreeEntryDto
            {
                Name = name,
                EntryType = typeof(ResourceModel).Name,
                ParentId = folder,
                IsFolder = true,
                AutoConnect = true,
            };

            AddTreeEntryCommand addTreeEntryCommand = new AddTreeEntryCommand
            {
                GameId = request.GameID,
                Player = request.Player,
                TreeEntryDto = addonFolder,
            };

            var result = await mediator.Send(addTreeEntryCommand);
            return result.Item2.FirstOrDefault(x => x.Name == name)?.Id;
        }

        private async Task FindAndInstallDependencies(InstallAddonCommand request, GameModel? game, AddonModel? addon)
        {
            if (addon.Dependencies == null)
            {
                return;
            }

            foreach (var dependency in addon.Dependencies)
            {
                var existingAddon = game.Addons.FirstOrDefault(x => x.Name == dependency.Name && CompareVersions(x, dependency));
                if (existingAddon == null)
                {
                    if (request.AutoInstallDeps == false)
                    {
                        throw new Exception("Dependency not found");
                    }

                    var nmAddon = await addonFromUriProvider.GetAddonByKey(dependency.Key, dependency.Version);

                    var (response, _) = await mediator.Send(new InstallAddonCommand
                    {
                        AddonFile = nmAddon,
                        AddonSourceKey = default,
                        AutoInstallDeps = true,
                        GameID = request.GameID,
                        Player = request.Player
                    });

                    if (response != CommandResponse.Ok)
                    {
                        throw new Exception("Cannot install dependency: " + dependency.Key);
                    }
                }
            }
        }

        private static bool CompareVersions(AddonModel x, AddonModel dependency)
        {
            return x.Version == dependency.Version;
        }

        private static byte[] ReadToBytes(ZipArchiveEntry? infoEntry)
        {
            using MemoryStream memoryStream = new MemoryStream();

            var stream = infoEntry.Open();
            stream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }
    }
}
