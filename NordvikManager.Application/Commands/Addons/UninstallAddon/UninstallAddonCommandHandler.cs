using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.Commands.Card;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Interfaces;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Addons.UninstallAddon
{
    internal class UninstallAddonCommandHandler : HandlerBase<UninstallAddonCommand, (CommandResponse, UninstallAddonCommandResponse)>
    {
        private readonly IMediator mediator;
        private readonly IGameEventLogger gameEventLogger;

        public UninstallAddonCommandHandler(IDbContext dbContext, IMediator mediator, IMapper mapper, IGameEventLogger gameEventLogger) : base(dbContext, mapper)
        {
            this.mediator = mediator;
            this.gameEventLogger = gameEventLogger;
        }

        public override async Task<(CommandResponse, UninstallAddonCommandResponse)> Handle(UninstallAddonCommand request, CancellationToken token)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            gameEventLogger.Info("AddonUninstall", $"[trace] handler start: addonId={request.AddonId} addonKey={request.AddonKey}");

            //Get game for futher processing
            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);
            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            // AsSplitQuery() — see InstallAddonCommandHandler.Handle's own comment
            // for why chaining multiple collection .Include()s without it is a
            // cartesian-explosion risk, same shape here for a single addon's own
            // Views/Resources/Templates/Actions counts.
            var addon = dbContext.Addons
                .Include(x => x.Views)
                .Include(x => x.Resources)
                .Include(x => x.Templates)
                .Include(x => x.Actions)
                .AsSplitQuery()
                .FirstOrDefault(x => x.Id == request.AddonId || x.Key == request.AddonKey);

            Guard.NotFound(addon, "Addon", (object?)request.AddonId ?? request.AddonKey);
            gameEventLogger.Info("AddonUninstall", $"[trace] addon loaded in {watch.ElapsedMilliseconds} ms: '{addon.Name}' key={addon.Key} — {addon.Resources.Count} resources, {addon.Views.Count} views, {addon.Templates.Count} templates, {addon.Actions.Count} actions");

            gameEventLogger.Info("AddonUninstall", $"Uninstalling addon '{addon.Name}' (ID: {addon.Id}) for game '{game.Name}' (ID: {game.Id}) by player '{request.Player.Name}' (ID: {request.Player.Id}).");

            //Remove all resources
            foreach (var resource in addon.Resources.ToList())
            {
                RemoveResourceCommand removeResourceCommand = new RemoveResourceCommand()
                {
                    Player = request.Player,
                    ID = resource.Id,
                    GameId = request.GameID
                };

                await mediator.Send(removeResourceCommand);
            }

            gameEventLogger.Info("AddonUninstall", $"[trace] resources removed ({watch.ElapsedMilliseconds} ms)");

            //Remove all views
            foreach (var view in addon.Views.ToList())
            {
                RemoveCardCommand removeCardCommand = new RemoveCardCommand()
                {
                    Player = request.Player,
                    Id = view.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeCardCommand);
            }

            gameEventLogger.Info("AddonUninstall", $"[trace] views removed ({watch.ElapsedMilliseconds} ms)");

            //Remove all templates
            foreach (var template in addon.Templates.ToList())
            {
                RemoveCardCommand removeCardCommand = new RemoveCardCommand()
                {
                    Player = request.Player,
                    Id = template.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeCardCommand);
            }

            gameEventLogger.Info("AddonUninstall", $"[trace] templates removed ({watch.ElapsedMilliseconds} ms)");

            //Remove all actions
            foreach (var action in addon.Actions.ToList())
            {
                RemoveActionCommand removeActionCommand = new RemoveActionCommand()
                {
                    Player = request.Player,
                    Id = action.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeActionCommand);
            }

            gameEventLogger.Info("AddonUninstall", $"[trace] actions removed ({watch.ElapsedMilliseconds} ms)");

            dbContext.Remove(addon);
            await dbContext.SaveChangesAsync();
            gameEventLogger.Info("AddonUninstall", $"[trace] addon row removed ({watch.ElapsedMilliseconds} ms)");

            // Bug fix: InstallAddonCommandHandler.CreateFolder builds "Addons/<AddonName>"
            // (plus "Scripts"/"Resources" subfolders) for every install, but removing an
            // addon's resources/actions/cards above only removes *their own* tree entries
            // (each RemoveResourceCommand/RemoveCardCommandHandler call does that) — the
            // containing folder entries themselves were never touched, so every uninstall
            // left empty "ghost" folders behind permanently in the Materials tree.
            await TryRemoveEmptyAddonFoldersAsync(request, addon.Name);
            gameEventLogger.Info("AddonUninstall", $"[trace] folders cleaned, handler done ({watch.ElapsedMilliseconds} ms)");

            return (CommandResponse.Ok, new UninstallAddonCommandResponse
            {
                AddonId = addon.Id,
                AddonKey = addon.Key,
                AddonName = addon.Name,
                AddonVersion = addon.Version
            });
        }

        /// <summary>
        /// Best-effort cleanup of the folder structure InstallAddonCommandHandler.CreateFolder
        /// creates for an addon. Tries child-before-parent (Scripts/Resources, then the addon's
        /// own folder, then the shared "Addons" root) since RemoveTreeEntryCommand refuses to
        /// delete a non-empty folder — if the GM dropped other files into one of these folders,
        /// or another addon still has its own folder under the shared root, that TreeException
        /// is caught and the folder is simply left in place rather than propagating a failure
        /// out of what is otherwise a successful uninstall.
        /// </summary>
        private async Task TryRemoveEmptyAddonFoldersAsync(UninstallAddonCommand request, string addonName)
        {
            var addonsRoot = dbContext.TreeEntries.FirstOrDefault(x =>
                x.Game.Id == request.GameID && x.Parent == null && x.IsFolder &&
                x.EntryType == nameof(ResourceModel) && x.Name == "Addons");
            if (addonsRoot == null) return;

            var addonFolder = dbContext.TreeEntries.FirstOrDefault(x =>
                x.Parent != null && x.Parent.Id == addonsRoot.Id && x.IsFolder &&
                x.EntryType == nameof(ResourceModel) && x.Name == addonName);
            if (addonFolder == null) return;

            var subFolders = dbContext.TreeEntries
                .Where(x => x.Parent != null && x.Parent.Id == addonFolder.Id && x.IsFolder)
                .Select(x => x.Id)
                .ToList();

            foreach (var subFolderId in subFolders)
                await TryRemoveFolderAsync(request, subFolderId);

            await TryRemoveFolderAsync(request, addonFolder.Id);
            await TryRemoveFolderAsync(request, addonsRoot.Id);
        }

        private async Task TryRemoveFolderAsync(UninstallAddonCommand request, Guid treeEntryId)
        {
            try
            {
                await mediator.Send(new RemoveTreeEntryCommand
                {
                    GameId = request.GameID,
                    PlayerId = request.Player.Id,
                    TreeEntryId = treeEntryId,
                });
            }
            catch (TreeException)
            {
                // Not empty — leave it.
            }
        }
    }
}
