using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Services
{
    /// <summary>Puts a new resource's content where it's kept: the database, or the server's file storage.</summary>
    internal static class ResourceContentStore
    {
        /// <param name="sourceFile">A finished upload's file: moved into file storage, never read.</param>
        public static async Task StoreAsync(ResourceModel model, IFileStorageProvider storage, ResourceStorageKind kind,
            byte[]? data, string? sourceFile, string? contentHash)
        {
            if (kind == ResourceStorageKind.ManagedFile)
            {
                model.Path = sourceFile != null
                    ? await storage.MoveInAsync(model.GameId, model.Id, sourceFile, null)
                    : await storage.SaveAsync(model.GameId, model.Id, data ?? Array.Empty<byte>(), null);
                model.Storage = ResourceStorageKind.ManagedFile;
            }
            else
            {
                model.Data = data ?? Array.Empty<byte>();
                model.Storage = ResourceStorageKind.Blob;
            }
            model.ContentHash = contentHash ?? ResourceVersions.HashOf(data ?? Array.Empty<byte>());
        }
    }
}
