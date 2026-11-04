using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Resources.GetResourceRange
{
    // Same lookup and versions as GetResourceContentCommandHandler, for a part of the bytes:
    // a file-backed resource is read only for that part. A database (Blob) resource can only
    // be loaded whole, then cut; big files belong in managed storage.
    public class GetResourceRangeCommandHandler : HandlerBase<GetResourceRangeCommand, ResourceRange?>
    {
        /// <summary>The most one request returns (2 MB; base64 over the data channel makes it ~2.7 MB).</summary>
        public const int MaxLength = 2 * 1024 * 1024;

        private readonly IFileStorageProvider storage;

        public GetResourceRangeCommandHandler(IMapper mapper, IDbContext ctx, IFileStorageProvider storage) : base(ctx, mapper)
        {
            this.storage = storage;
        }

        private record Meta(Guid Id, MimeType MimeType, ResourceStorageKind Storage, string? Path, string? ContentHash, long BlobLength);

        public async override Task<ResourceRange?> Handle(GetResourceRangeCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var meta = await dbContext.Resources
                .Where(x => x.GameId == request.GameID &&
                    (x.Id == request.ID || (x.Key != null && request.Key != null && x.Key == request.Key)))
                .Select(x => new Meta(x.Id, x.MimeType, x.Storage, x.Path, x.ContentHash, x.Data == null ? 0 : x.Data.Length))
                .FirstOrDefaultAsync(cancellationToken);
            if (meta == null)
                return null;

            long total;
            string? version;
            byte[]? whole = null;
            if (meta.Storage == ResourceStorageKind.Blob)
            {
                total = meta.BlobLength;
                version = meta.ContentHash;
            }
            else
            {
                var stamp = storage.GetStamp(meta.Path);
                if (stamp == null)
                    return null; // file missing
                total = stamp.Length;
                // Can be edited on disk outside the app: versioned by its file stamp.
                version = meta.Storage == ResourceStorageKind.Linked ? ResourceVersions.OfStamp(stamp) : meta.ContentHash;
            }

            if (version == null)
            {
                // Written before ContentHash existed: hash it once, as GetResourceContent does.
                whole = await ReadWholeAsync(meta, cancellationToken);
                if (whole == null)
                    return null;
                version = ResourceVersions.HashOf(whole);
                var stub = dbContext.Resources.Local.FirstOrDefault(x => x.Id == meta.Id);
                if (stub == null) { stub = new ResourceModel { Id = meta.Id }; dbContext.Resources.Attach(stub); }
                stub.ContentHash = version;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (request.IfVersion == version)
                return new ResourceRange(null, request.Start, total, meta.MimeType, version, true);

            var start = Math.Max(0, request.Start);
            var length = (int)Math.Min(Math.Max(0, request.Length), Math.Min(MaxLength, Math.Max(0, total - start)));
            if (length == 0)
                return new ResourceRange(Array.Empty<byte>(), start, total, meta.MimeType, version, false);

            byte[]? data;
            if (meta.Storage == ResourceStorageKind.Blob && whole == null
                && (data = await dbContext.ReadResourceSliceAsync(meta.Id, start, length, cancellationToken)) != null)
            {
                // cut by the database
            }
            else if (meta.Storage == ResourceStorageKind.Blob || whole != null)
            {
                whole ??= await ReadWholeAsync(meta, cancellationToken);
                if (whole == null)
                    return null;
                data = whole.AsSpan((int)start, Math.Min(length, whole.Length - (int)start)).ToArray();
            }
            else
            {
                data = await storage.ReadRangeAsync(meta.Path!, start, length);
                if (data == null)
                    return null;
            }
            return new ResourceRange(data, start, total, meta.MimeType, version, false);
        }

        private async Task<byte[]?> ReadWholeAsync(Meta meta, CancellationToken cancellationToken) =>
            meta.Storage == ResourceStorageKind.Blob
                ? await dbContext.Resources.Where(x => x.Id == meta.Id).Select(x => x.Data).FirstAsync(cancellationToken)
                : await storage.ReadAsync(meta.Path!);
    }
}
