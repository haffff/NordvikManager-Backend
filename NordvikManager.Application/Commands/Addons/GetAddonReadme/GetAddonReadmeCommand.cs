using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;

namespace NordvikManager.Application.Commands.Addons.GetAddonReadme
{
    /// <summary>
    /// An addon's markdown readme, by its registry key. The URL comes from the registry entry
    /// (IAddonRepositoryService.GetReadmeByKey), never from the caller. Null when it has none.
    /// </summary>
    public class GetAddonReadmeCommand : CommandBase<AddonReadmeDto?>
    {
        public string Key { get; set; } = "";
    }

    internal class GetAddonReadmeCommandHandler : IRequestHandler<GetAddonReadmeCommand, AddonReadmeDto?>
    {
        private readonly IAddonRepositoryService addonRepository;

        public GetAddonReadmeCommandHandler(IAddonRepositoryService addonRepository)
        {
            this.addonRepository = addonRepository;
        }

        public async Task<AddonReadmeDto?> Handle(GetAddonReadmeCommand request, CancellationToken cancellationToken)
        {
            var readme = await addonRepository.GetReadmeByKey(request.Key);
            return readme == null ? null : new AddonReadmeDto { Key = request.Key, Url = readme.Url, Markdown = readme.Markdown };
        }
    }
}
