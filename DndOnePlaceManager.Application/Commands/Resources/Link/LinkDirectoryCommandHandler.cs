using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.Link
{
    internal class LinkDirectoryCommandHandler : HandlerBase<LinkDirectoryCommand, (CommandResponse, int)>
    {
        private readonly IMediator mediator;

        public LinkDirectoryCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator)
            : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, int)> Handle(LinkDirectoryCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);

            var player = await dbContext.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
            Guard.NotFound(player, "Player", playerId);

            var isGM = player.System || game.MasterId == playerId;
            if (!isGM)
                throw new PermissionException(Permission.Edit);

            if (!Directory.Exists(request.LocalDirectoryPath))
                throw new ResourceNotFoundException("LocalDirectoryPath", request.LocalDirectoryPath);

            var rootPath = Path.GetFullPath(request.LocalDirectoryPath);
            var folderIdsByRelativePath = new Dictionary<string, Guid?> { [string.Empty] = request.ParentFolder };
            var linkedCount = 0;

            foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                var mimeType = filePath.ToMimeType();
                // Skip files we can't classify (Thumbs.db, .DS_Store, desktop.ini, etc.) rather
                // than clutter the tree with junk resources — a single file can still be linked
                // explicitly via LinkResourceCommand regardless of MimeType.
                if (mimeType == null || mimeType == MimeType.None)
                    continue;

                var relativeDir = Path.GetDirectoryName(Path.GetRelativePath(rootPath, filePath)) ?? string.Empty;
                var parentFolderId = await EnsureFolderPath(request, relativeDir, folderIdsByRelativePath, cancellationToken);

                var model = new ResourceModel
                {
                    Id       = Guid.NewGuid(),
                    Name     = Path.GetFileName(filePath),
                    Path     = filePath,
                    Storage  = ResourceStorageKind.Linked,
                    MimeType = mimeType.Value,
                    Key      = null,
                    GameId   = request.GameId,
                    PlayerId = player.Id,
                    Player   = player,
                };

                await dbContext.Resources.AddAsync(model, cancellationToken);
                dbContext.SaveChanges();

                await mediator.Send(new AddTreeEntryCommand
                {
                    GameId = request.GameId,
                    Player = request.Player,
                    TreeEntryDto = new TreeEntryDto
                    {
                        Name      = model.Name,
                        EntryType = typeof(ResourceModel).Name,
                        IsFolder  = false,
                        TargetId  = model.Id,
                        ParentId  = parentFolderId,
                    },
                }, cancellationToken);

                linkedCount++;

                // Throttled so a 5,000-file folder doesn't fire 5,000 broadcasts.
                if (linkedCount % 10 == 0)
                    request.OnProgress?.Invoke(linkedCount);
            }

            request.OnProgress?.Invoke(linkedCount);

            return (CommandResponse.Ok, linkedCount);
        }

        // Creates (and caches) the chain of tree folders needed to mirror a relative on-disk
        // subfolder path, so linked files land organized in the tree instead of dumped flat.
        private async Task<Guid?> EnsureFolderPath(
            LinkDirectoryCommand request,
            string relativeDir,
            Dictionary<string, Guid?> cache,
            CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(relativeDir, out var cached))
                return cached;

            var parentRelative = Path.GetDirectoryName(relativeDir) ?? string.Empty;
            var parentId = await EnsureFolderPath(request, parentRelative, cache, cancellationToken);

            var (_, entries) = await mediator.Send(new AddTreeEntryCommand
            {
                GameId = request.GameId,
                Player = request.Player,
                TreeEntryDto = new TreeEntryDto
                {
                    Name      = Path.GetFileName(relativeDir),
                    EntryType = typeof(ResourceModel).Name,
                    IsFolder  = true,
                    ParentId  = parentId,
                },
            }, cancellationToken);

            var folderId = entries.FirstOrDefault()?.Id;
            cache[relativeDir] = folderId;
            return folderId;
        }
    }
}
