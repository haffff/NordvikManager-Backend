using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.SetResource
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

                if (mimeType != MimeType.None)
                    existing.MimeType = mimeType;
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
                var isGM = player.System || dbContext.Games.Any(g => g.Id == request.GameId && g.MasterId == player.Id);
                if (!isGM)
                    throw new PermissionException(Permission.Edit);

                model.Path = await storage.SaveAsync(model.GameId, model.Id, request.Data, null);
                model.Storage = ResourceStorageKind.ManagedFile;
            }
            else
            {
                model.Data = request.Data;
                model.Storage = ResourceStorageKind.Blob;
            }

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
