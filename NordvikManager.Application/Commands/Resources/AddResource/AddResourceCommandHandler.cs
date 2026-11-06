using NordvikManager.Application.Services;

using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Resources
{
    public class AddImageCommandHandler : HandlerBase<AddResourceCommand, (CommandResponse, Guid?)>
    {
        private readonly IMediator mediator;
        private readonly IFileStorageProvider storage;
        private readonly UploadQuota? quota;
        public AddImageCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage, UploadQuota? quota = null) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
            this.storage = storage;
            this.quota = quota;
        }

        public async override Task<(CommandResponse, Guid?)> Handle(AddResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            byte[]? data = request.SourceFile != null ? null : request.DataRaw ?? Convert.FromBase64String(request.Data);

            //TODO: mimeType enum should be passed in command
            MimeType? mimeType = request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>();

            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);

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

            // Files go into the server's own folder, so editing the game is enough. (A finished
            // upload was checked when it started: big uploads go there for anyone.)
            if (request.StorageKind == ResourceStorageKind.ManagedFile && request.SourceFile == null
                && (game == null || !game.HasPermission(player.Id, Permission.Edit)))
                throw new PermissionException(Permission.Edit);
            if (!request.SkipQuota && quota != null)
                await quota.EnsureRoomAsync(dbContext, model.GameId, player.Id, data?.Length ?? 0, cancellationToken: cancellationToken);
            await ResourceContentStore.StoreAsync(model, storage, request.StorageKind, data, request.SourceFile, request.ContentHash);

            var entry = await dbContext.Resources.AddAsync(model);

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

