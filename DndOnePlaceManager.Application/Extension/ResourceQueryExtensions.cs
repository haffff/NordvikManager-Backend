using DndOnePlaceManager.Domain.Entities.Resources;

namespace DndOnePlaceManager.Application.Extension
{
    public static class ResourceQueryExtensions
    {
        /// <summary>
        /// Resource rows without their file bytes (Data, ThumbnailData), untracked. For listings:
        /// a Blob-stored resource keeps the whole file in Data.
        /// </summary>
        public static IQueryable<ResourceModel> WithoutFileData(this IQueryable<ResourceModel> query) =>
            query.Select(r => new ResourceModel
            {
                Id = r.Id,
                GameId = r.GameId,
                PlayerId = r.PlayerId,
                Name = r.Name,
                Key = r.Key,
                MimeType = r.MimeType,
                Storage = r.Storage,
                Path = r.Path,
                Volume = r.Volume,
            });
    }
}
