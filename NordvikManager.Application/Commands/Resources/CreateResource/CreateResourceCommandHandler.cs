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

namespace NordvikManager.Application.Commands.Resources.CreateResource
{
    internal class CreateResourceCommandHandler : HandlerBase<CreateResourceCommand, (CommandResponse, Guid?)>
    {
        private readonly IMediator _mediator;
        private readonly IFileStorageProvider storage;

        public CreateResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            _mediator = mediator;
            this.storage = storage;
        }

        public override async Task<(CommandResponse, Guid?)> Handle(CreateResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.Key))
            {
                var existing = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken);
                if (existing != null)
                    return (CommandResponse.AlreadyExists, null);
            }

            var player = await dbContext.Players.FirstOrDefaultAsync(
                p => p.Id == request.Player.Id, cancellationToken);
            // Player identity can't be confirmed — treat as a permission failure (403),
            // not a resource-not-found (404): the resource being created doesn't exist yet.
            if (player == null)
                throw new PermissionException(Permission.Read);

            var mimeType = request.MimeType?.ToEnumUsingDescriptionAttribute<MimeType>() ?? MimeType.None;

            var model = new ResourceModel
            {
                Id       = Guid.NewGuid(),
                Name     = request.Name,
                Key      = string.IsNullOrWhiteSpace(request.Key) ? null : request.Key,
                MimeType = mimeType,
                GameId   = request.GameId,
                PlayerId = player.Id,
                Player   = player,
            };

            // Files go into the server's own folder, so editing the game is enough. (A finished
            // upload was checked when it started: big uploads go there for anyone.)
            if (request.StorageKind == ResourceStorageKind.ManagedFile && request.SourceFile == null)
            {
                var game = dbContext.Games.FirstOrDefault(g => g.Id == request.GameId);
                if (game == null || !game.HasPermission(player.Id, Permission.Edit))
                    throw new PermissionException(Permission.Edit);
            }
            await ResourceContentStore.StoreAsync(model, storage, request.StorageKind, request.Data, request.SourceFile, request.ContentHash);

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

            return (CommandResponse.Ok, model.Id);
        }
    }
}
