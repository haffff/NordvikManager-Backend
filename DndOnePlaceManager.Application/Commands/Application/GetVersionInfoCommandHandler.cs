using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Application
{
    internal class GetVersionInfoCommandHandler : HandlerBase<GetVersionInfoCommand, VersionInfoDTO>
    {
        const string Version = "0.0.0";

        private readonly IVersionService versionService;

        public GetVersionInfoCommandHandler(IDbContext dbContext, IMapper mapper, IVersionService versionService) : base(dbContext, mapper)
        {
            this.versionService = versionService;
        }

        public override async Task<VersionInfoDTO> Handle(GetVersionInfoCommand request, CancellationToken cancellationToken)
        {
            var versionRawString = await versionService.GetVersionJSONAsync();
            var versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfoDTO>(versionRawString);
            if (versionInfo == null)
            {
                throw new Exception("Error getting version from github");
            }

            var currentVersion = new Version(Version);
            var latestVersion = new Version(versionInfo.CurrentVersion);

            versionInfo.Version = Version;
            versionInfo.IsUpdateAvailable = currentVersion < latestVersion;

            return versionInfo;
        }
    }
}
