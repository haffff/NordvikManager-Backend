using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Infrastructure.Services
{
    internal class VersionService : IVersionService
    {
        public VersionService()
        {
        }

        public async Task<string> GetVersionJSONAsync()
        {
            using HttpClient client = new HttpClient();
            var response = await client.GetAsync("https://raw.githubusercontent.com/haffff/nordvikmanager/main/version.json");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                throw new Exception("Error getting version from github");
            }
        }
    }
}
