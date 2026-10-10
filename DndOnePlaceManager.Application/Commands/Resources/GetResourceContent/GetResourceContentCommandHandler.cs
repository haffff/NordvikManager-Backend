using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.GetResourceContent
{
    public class GetResourceContentCommandHandler : HandlerBase<GetResourceContentCommand, ResourceContent?>
    {
        private readonly IFileStorageProvider storage;

        public GetResourceContentCommandHandler(IMapper mapper, IDbContext ctx, IFileStorageProvider storage) : base(ctx, mapper)
        {
            this.storage = storage;
        }

        private record Meta(Guid Id, MimeType MimeType, ResourceStorageKind Storage, string? Path,
            string? ContentHash, string? ThumbnailSourceVersion, bool HasThumbnail);

        public async override Task<ResourceContent?> Handle(GetResourceContentCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // Everything but the bytes, so an unchanged resource costs no file read.
            // GameId stays in the predicate: a Key is only unique per game.
            var meta = await dbContext.Resources
                .Where(x => x.GameId == request.GameID &&
                    (x.Id == request.ID || (x.Key != null && request.Key != null && x.Key == request.Key)))
                .Select(x => new Meta(x.Id, x.MimeType, x.Storage, x.Path, x.ContentHash, x.ThumbnailSourceVersion, x.ThumbnailData != null))
                .FirstOrDefaultAsync(cancellationToken);
            if (meta == null)
                return null;

            // Written to through a stub, so saving a hash or thumbnail never loads Data.
            ResourceModel? stub = null;
            ResourceModel Stub()
            {
                if (stub == null)
                {
                    stub = dbContext.Resources.Local.FirstOrDefault(x => x.Id == meta.Id);
                    if (stub == null)
                    {
                        stub = new ResourceModel { Id = meta.Id };
                        dbContext.Resources.Attach(stub);
                    }
                }
                return stub;
            }

            byte[]? data = null;
            string? version;
            if (meta.Storage == ResourceStorageKind.Linked)
            {
                // Can be edited on disk outside the app: versioned by its file stamp.
                version = ResourceVersions.OfStamp(storage.GetStamp(meta.Path));
            }
            else if (meta.ContentHash != null)
            {
                version = meta.ContentHash;
            }
            else
            {
                // Written before ContentHash existed: hash it once.
                data = await ReadAsync(meta, cancellationToken);
                if (data == null)
                    return null;
                version = ResourceVersions.HashOf(data);
                Stub().ContentHash = version;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            if (version == null)
                return null; // linked file missing

            if (!request.Thumbnail)
            {
                if (request.IfVersion == version)
                    return new ResourceContent(null, meta.MimeType, version, true);
                data ??= await ReadAsync(meta, cancellationToken);
                return data == null ? null : new ResourceContent(data, meta.MimeType, version, false);
            }

            var thumbnailVersion = "t-" + version;
            if (request.IfVersion == thumbnailVersion)
                return new ResourceContent(null, MimeType.PNG, thumbnailVersion, true);

            if (meta.HasThumbnail && meta.ThumbnailSourceVersion == version)
            {
                var stored = await dbContext.Resources.Where(x => x.Id == meta.Id).Select(x => x.ThumbnailData).FirstAsync(cancellationToken);
                return new ResourceContent(stored, MimeType.PNG, thumbnailVersion, false);
            }

            // No thumbnail yet, or it was made from an older version of the file.
            data ??= await ReadAsync(meta, cancellationToken);
            if (data == null)
                return null;

            // Not a raster type that can be thumbnailed (audio, html, ...): the original.
            if (!ThumbnailHelper.IsImage(meta.MimeType))
                return new ResourceContent(data, meta.MimeType, thumbnailVersion, false);

            byte[] thumbnail;
            try
            {
                thumbnail = ThumbnailHelper.Generate(data, request.MaxDimension);
            }
            catch
            {
                // Corrupt/unsupported image data: the original rather than failing.
                return new ResourceContent(data, meta.MimeType, thumbnailVersion, false);
            }

            var row = Stub();
            row.ThumbnailData = thumbnail;
            row.ThumbnailSourceVersion = version;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ResourceContent(thumbnail, MimeType.PNG, thumbnailVersion, false);
        }

        // A file-backed resource can go missing (moved, deleted, drive unplugged): null.
        private async Task<byte[]?> ReadAsync(Meta meta, CancellationToken cancellationToken) =>
            meta.Storage == ResourceStorageKind.Blob
                ? await dbContext.Resources.Where(x => x.Id == meta.Id).Select(x => x.Data).FirstAsync(cancellationToken)
                : await storage.ReadAsync(meta.Path);
    }
}
