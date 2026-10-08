using AutoMapper;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourceThumbnailCommandHandler : HandlerBase<GetResourceThumbnailCommand, (byte[], MimeType)>
    {
        private readonly IFileStorageProvider storage;

        public GetResourceThumbnailCommandHandler(IMapper mapper, IDbContext ctx, IFileStorageProvider storage) : base(ctx, mapper)
        {
            this.storage = storage;
        }

        public async override Task<(byte[], MimeType)> Handle(GetResourceThumbnailCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // Same lookup as GetResourceDataCommandHandler — GameId must stay part of
            // the predicate itself since a Key is only unique per-game.
            var image = await dbContext.Resources.FirstOrDefaultAsync(x =>
                x.GameId == request.GameID &&
                (x.Id == request.ID || (x.Key != null && request.Key != null && x.Key == request.Key)));

            if (image == null)
                return (null, MimeType.None);

            // Cache hit — never re-decode/re-resize an already-thumbnailed image.
            if (image.ThumbnailData != null)
                return (image.ThumbnailData, MimeType.PNG);

            var data = image.Storage == ResourceStorageKind.Blob
                ? image.Data
                : await storage.ReadAsync(image.Path);

            if (data == null)
                return (null, MimeType.None);

            // Not a raster type ImageSharp can decode (audio, html, ...) — nothing to
            // thumbnail, hand back the original so the caller still gets something.
            if (!ThumbnailHelper.IsImage(image.MimeType))
                return (data, image.MimeType);

            try
            {
                var thumbnail = ThumbnailHelper.Generate(data, request.MaxDimension);
                image.ThumbnailData = thumbnail;
                dbContext.SaveChanges();
                return (thumbnail, MimeType.PNG);
            }
            catch
            {
                // Corrupt/unsupported image data — fall back to the original rather
                // than failing the request outright.
                return (data, image.MimeType);
            }
        }
    }
}
