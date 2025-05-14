namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    public interface IVersionService
    {
        public Task<string> GetVersionJSONAsync();
    }
}