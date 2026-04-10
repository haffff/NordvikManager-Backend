namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    /// <summary>
    /// Provides access to the configured addon repository.
    /// All URLs are resolved internally — no external URL is ever accepted from callers.
    /// </summary>
    public interface IAddonRepositoryService
    {
        /// <summary>Downloads the addon zip by its registry key.</summary>
        Task<byte[]> GetAddonByKey(string key);

        /// <summary>Returns the raw repository JSON from the configured MainRepository.</summary>
        Task<string> GetRepository();
    }
}
