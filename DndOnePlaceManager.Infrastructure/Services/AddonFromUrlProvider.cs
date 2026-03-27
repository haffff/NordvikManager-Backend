using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net;

namespace DndOnePlaceManager.Infrastructure.Services
{
    internal class AddonRepositoryService : IAddonRepositoryService
    {
        private static HttpClient httpClient { get; set; }

        ////To be used later on
        //private static Dictionary<string, string> repositoryResourcesCache { get; set; }

        string[] trustedRepositories = new string[] { };

        string mainRepository;

        bool canAccessNotAllowedRepository = false;

        public AddonRepositoryService(IConfiguration configuration)
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
            }

            mainRepository = configuration["AddonsConfiguration:MainRepository"];
            trustedRepositories = configuration.GetSection("AddonsConfiguration:TrustedRepositories").Get<string[]>();
            canAccessNotAllowedRepository = configuration.GetValue<bool?>("AddonsConfiguration:CanAccessNotAllowedRepository") ?? false;
        }

        public async Task<byte[]> GetAddonByKey(string key, string? version)
        {
            var url = await GetAddonUrl(key);

            //Just download the file
            var result = await httpClient.GetAsync(url);
            if (result.StatusCode == HttpStatusCode.OK)
            {
                return await result.Content.ReadAsByteArrayAsync();
            }
            else
            {
                throw new Exception("Addon not found");
            }
        }

        public async Task<string> GetRepository(string? repository)
        {
            if (repository == null)
            {
                if (mainRepository == null)
                {
                    throw new Exception("Main repository is not set");
                }

                var result = await httpClient.GetStringAsync(mainRepository);

                return result;
            }

            //check if provided repository is in trusted repositories
            if (trustedRepositories.Contains(repository) || canAccessNotAllowedRepository)
            {
                return await httpClient.GetStringAsync(repository);
            }

            throw new Exception("Repository is not trusted");
        }

        private async Task<string> GetAddonUrl(string key)
        {
            foreach (var repo in trustedRepositories)
            {
                var repositoryResult = await httpClient.GetAsync(repo);
                if (repositoryResult.StatusCode == HttpStatusCode.OK)
                {
                    var repositoryContent = await repositoryResult.Content.ReadAsStringAsync();
                    var jobject = JObject.Parse(repositoryContent);
                    var repository = jobject["repository"];
                    var foundAddon = repository.FirstOrDefault(x => x["key"].ToString() == key);
                    if (foundAddon != null)
                    {
                        return foundAddon["releaseUrl"].ToString();
                    }
                }
            }

            return null;
        }
    }
}
