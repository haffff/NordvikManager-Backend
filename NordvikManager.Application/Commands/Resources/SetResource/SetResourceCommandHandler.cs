using NordvikManager.Application.Services;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Resources.SetResource
{
    public class SetResourceCommandHandler : HandlerBase<SetResourceCommand, Guid>
    {
        private readonly IMediator _mediator;
        private readonly IFileStorageProvider storage;

        public SetResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            _mediator = mediator;
            this.storage = storage;
        }

        public override async Task<Guid> Handle(SetResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var existing = await dbContext.Resources.FirstOrDefaultAsync(r =>
                r.GameId == request.GameId && r.Key == request.Key, cancellationToken);

            var mimeType = request.MimeType?.ToEnumUsingDescriptionAttribute<MimeType>() ?? MimeType.None;

            if (existing != null)
            {
                if (existing.Storage == ResourceStorageKind.Linked)
                    throw new WrongArgumentsException(nameof(request.Key));

                if (existing.Storage == ResourceStorageKind.ManagedFile)
                    existing.Path = await storage.SaveAsync(existing.GameId, existing.Id, request.Data, null);
                else
                    existing.Data = request.Data;
                existing.ContentHash = ResourceVersions.HashOf(request.Data);

                if (mimeType != MimeType.None)
                    existing.MimeType = mimeType;

                // A player's upload that happened to have this key: what's written now is the
                // writer's, or the player could rewrite it later (a script an action runs).
                var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == existing.GameId, cancellationToken);
                if (game != null && request.Player?.Id is Guid writerId && !game.IsRunBy(existing.PlayerId))
                    existing.PlayerId = writerId;

                dbContext.SaveChanges();
                return existing.Id;
            }

            var player = await dbContext.Players.FirstOrDefaultAsync(p =>
                p.Id == request.Player.Id, cancellationToken);

            var model = new ResourceModel
            {
                Id       = Guid.NewGuid(),
                Name     = request.Name ?? request.Key,
                Key      = request.Key,
                MimeType = mimeType,
                GameId   = request.GameId,
                PlayerId = player.Id,
                Player   = player,
            };

            if (request.StorageKind == ResourceStorageKind.ManagedFile)
            {
                // Files go into the server's own folder, so editing the game is enough.
                var game = dbContext.Games.FirstOrDefault(g => g.Id == request.GameId);
                if (game == null || !game.HasPermission(player.Id, Permission.Edit))
                    throw new PermissionException(Permission.Edit);

                model.Path = await storage.SaveAsync(model.GameId, model.Id, request.Data, null);
                model.Storage = ResourceStorageKind.ManagedFile;
            }
            else
            {
                model.Data = request.Data;
                model.Storage = ResourceStorageKind.Blob;
            }
            model.ContentHash = ResourceVersions.HashOf(request.Data);

            await dbContext.Resources.AddAsync(model, cancellationToken);
            dbContext.SaveChanges();

            await _mediator.Send(new AddTreeEntryCommand
            {
                GameId = request.GameId,
                Player = request.Player,
                TreeEntryDto = new TreeEntryDto
                {
                    Name      = model.Name,
                    EntryType = typeof(ResourceModel).Name,
                    IsFolder  = false,
                    TargetId  = model.Id,
                    ParentId  = request.ParentFolder,
                },
            }, cancellationToken);

            return model.Id;
        }
    }
}
