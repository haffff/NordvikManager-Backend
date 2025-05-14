namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IAddonRepositoryService
    {
        public Task<byte[]> GetAddonByKey(string uri, string? version = null);
        public Task<string> GetRepository(string? repository = null);
    }
}
