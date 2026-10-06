using DndOnePlaceManager.Infrastructure.Interfaces;
using System.Security.Cryptography;

namespace DndOnePlaceManager.Application.Services
{
    /// <summary>
    /// The version clients cache a resource's bytes under. Files the app writes itself
    /// (Blob, ManagedFile) use a hash of the content; a linked file — which can be edited
    /// on disk outside the app — uses its last write time and size.
    /// </summary>
    public static class ResourceVersions
    {
        public static string HashOf(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        public static string? OfStamp(FileStamp? stamp) =>
            stamp == null ? null : $"lm-{stamp.LastWriteUtc.Ticks}-{stamp.Length}";
    }
}
