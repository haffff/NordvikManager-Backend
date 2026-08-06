
using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourceDataCommandHandler : HandlerBase<GetResourceDataCommand, (byte[], MimeType)>
    {
        private readonly IFileStorageProvider storage;

        public GetResourceDataCommandHandler(IMapper mapper, IDbContext ctx, IFileStorageProvider storage) : base(ctx, mapper)
        {
            this.storage = storage;
        }

        public async override Task<(byte[], MimeType)> Handle(GetResourceDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // GameId must be part of the predicate itself, not a filter applied after
            // FirstOrDefaultAsync — a resource Key is only unique per-game (see
            // SetResourceCommandHandler's (GameId, Key) existence check), so the same
            // key can legitimately exist under multiple games. Checking GameId only
            // after picking an arbitrary matching row meant a stale/other game's row
            // could shadow the current game's own resource and produce a false 404.
            // GameId must be part of the predicate itself, not a filter applied after
            // FirstOrDefaultAsync — a resource Key is only unique per-game (see
            // SetResourceCommandHandler's (GameId, Key) existence check), so the same
            // key can legitimately exist under multiple games. Checking GameId only
            // after picking an arbitrary matching row meant a stale/other game's row
            // could shadow the current game's own resource and produce a false 404.
            var image = await dbContext.Resources.FirstOrDefaultAsync(x =>
                x.GameId == request.GameID &&
                (x.Id == request.ID || (x.Key != null && request.Key != null && x.Key == request.Key)));
            var player = await dbContext.Players.FirstOrDefaultAsync(x => x.Id == request.Player.Id);

            //Todo - when getting from game. check for player

            //if (image != null && (image.PlayerId == request.Player.Id || player.System))
            if (image != null)
            {
                // Blob bytes are always available; a file-backed resource (managed or linked)
                // can go missing out from under us (moved, deleted, drive unplugged for a linked
                // path) — ReadAsync returns null rather than throwing, and that's treated exactly
                // like "resource not found" rather than surfacing a 500.
                var data = image.Storage == ResourceStorageKind.Blob
                    ? image.Data
                    : await storage.ReadAsync(image.Path);

                if (data != null)
                    return (data, image.MimeType);
            }

            return (null, MimeType.None);
        }
    }
}

