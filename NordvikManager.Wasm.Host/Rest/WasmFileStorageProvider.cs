using DndOnePlaceManager.Infrastructure.Interfaces;

namespace NordvikManager.Wasm.Host.Rest
{
    /// <summary>
    /// Stub IFileStorageProvider for the wasm host. The real LocalFileStorageProvider
    /// (DndOnePlaceManager.Infrastructure) writes to the actual local disk of whoever runs
    /// the backend — meaningless for a browser-wasm host, same reasoning as excluding the
    /// filesystem-linking REST actions (LinkResource/LinkDirectory/BrowseLocalDirectory).
    /// Registered only so MediatR/DI can construct handlers that take IFileStorageProvider
    /// in their constructor (e.g. CreateResourceCommandHandler) — every REST route ported
    /// here uses ResourceStorageKind.Blob (bytes stored directly in the EF Core InMemory
    /// database), which never actually calls into this provider. If a caller somehow
    /// requests ManagedFile storage, fail loudly rather than silently no-op.
    /// </summary>
    public class WasmFileStorageProvider : IFileStorageProvider
    {
        private static NotSupportedException NotSupported() =>
            new("File-based storage is not available under browser-wasm — use ResourceStorageKind.Blob.");

        public Task<string> SaveAsync(Guid gameId, Guid resourceId, byte[] data, string? fileExtension) => throw NotSupported();
        public Task<byte[]?> ReadAsync(string path) => throw NotSupported();
        public Task DeleteAsync(string path) => throw NotSupported();
        public bool Exists(string path) => throw NotSupported();
        public IReadOnlyList<LocalDirectoryEntry> ListDirectory(string? path) => throw NotSupported();
    }
}
