
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

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class AddImageCommandHandler : HandlerBase<AddResourceCommand, (CommandResponse, Guid?)>
    {
        private readonly IMediator mediator;
        private readonly IFileStorageProvider storage;
        public AddImageCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
            this.storage = storage;
        }

        public async override Task<(CommandResponse, Guid?)> Handle(AddResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            byte[] data = request.DataRaw ?? Convert.FromBase64String(request.Data);

            //TODO: mimeType enum should be passed in command
            MimeType? mimeType = request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>();

            var game = dbContext.Games.Include(x => x.Resources).Include(x => x.TreeEntries).FirstOrDefault(x => x.Id == request.GameID);

            var player = dbContext.Players.FirstOrDefault(x => x.Id == request.Player.Id);
            Guard.NotFound(player, "Player", request.Player.Id);

            var model = new ResourceModel()
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Player = player,
                PlayerId = player.Id,
                MimeType = mimeType ?? MimeType.None,
                Key = request.Key,
                GameId = request.GameID ?? Guid.Empty,
            };

            if (request.StorageKind == ResourceStorageKind.ManagedFile)
            {
                var isGM = player.System || (game != null && game.MasterId == player.Id);
                if (!isGM)
                    throw new PermissionException(Permission.Edit);

                model.Path = await storage.SaveAsync(model.GameId, model.Id, data, null);
                model.Storage = ResourceStorageKind.ManagedFile;
            }
            else
            {
                model.Data = data;
                model.Storage = ResourceStorageKind.Blob;
            }

            var entry = await dbContext.Resources.AddAsync(model);
            game?.Resources.Add(entry.Entity);

            var result = dbContext.SaveChanges();

            TreeEntryDto treeEntry = new TreeEntryDto
            {
                Name = model.Name,
                EntryType = typeof(ResourceModel).Name,
                IsFolder = false,
                TargetId = model.Id,
                ParentId = request.ParentFolder
            };

            var newResult = await mediator.Send(new AddTreeEntryCommand()
            {
                TreeEntryDto = treeEntry,
                GameId = request.GameID,
                Player = request.Player
            });

            return (CommandResponse.Ok, entry.Entity.Id);
        }
    }
}

