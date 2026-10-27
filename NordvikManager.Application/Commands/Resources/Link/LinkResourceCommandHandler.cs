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

namespace NordvikManager.Application.Commands.Resources.Link
{
    internal class LinkResourceCommandHandler : HandlerBase<LinkResourceCommand, (CommandResponse, Guid?)>
    {
        private readonly IMediator mediator;
        private readonly IFileStorageProvider storage;

        public LinkResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            this.mediator = mediator;
            this.storage = storage;
        }

        public override async Task<(CommandResponse, Guid?)> Handle(LinkResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // The host's disk belongs to whoever runs this server, not to a game's GM.
            if (!request.IsLocalAdmin)
                throw new PermissionException(Permission.Edit);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);

            var player = await dbContext.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
            Guard.NotFound(player, "Player", playerId);

            // Normalize to an absolute path before persisting — a relative LocalPath would
            // resolve against the backend process's working directory, which can change
            // between the link and any later read and silently point at a different file.
            var normalizedPath = System.IO.Path.GetFullPath(request.LocalPath);

            if (!storage.Exists(normalizedPath))
                throw new ResourceNotFoundException("LocalPath", request.LocalPath);

            MimeType? mimeTypeOrNull = !string.IsNullOrWhiteSpace(request.MimeType)
                ? request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>()
                : normalizedPath.ToMimeType();
            var mimeType = mimeTypeOrNull ?? MimeType.None;

            var model = new ResourceModel
            {
                Id       = Guid.NewGuid(),
                Name     = request.Name,
                Path     = normalizedPath,
                Storage  = ResourceStorageKind.Linked,
                MimeType = mimeType,
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
                    ParentId  = request.ParentFolder,
                },
            }, cancellationToken);

            return (CommandResponse.Ok, model.Id);
        }
    }
}
