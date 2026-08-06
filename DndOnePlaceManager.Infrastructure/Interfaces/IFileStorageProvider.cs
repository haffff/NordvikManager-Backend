namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    // file:// storage today; deliberately path-in/bytes-out shaped (not DB-aware, not
    // game-object-aware beyond the save call) so a future S3/Azure provider can implement the
    // same interface without touching any Application-layer handler.
    public interface IFileStorageProvider
    {
        Task<string> SaveAsync(Guid gameId, Guid resourceId, byte[] data, string? fileExtension);
        Task<byte[]?> ReadAsync(string path);
        Task DeleteAsync(string path);
        bool Exists(string path);
        IReadOnlyList<LocalDirectoryEntry> ListDirectory(string? path);
    }
}
