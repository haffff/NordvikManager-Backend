using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository
{
    internal class GetAddonsFromRepositoryCommandHandler : IRequestHandler<GetAddonsFromRepositoryCommand, List<AddonDto>>
    {
        private readonly IMapper mapper;
        private readonly IAddonRepositoryService addonRepository;

        public GetAddonsFromRepositoryCommandHandler(IMapper mapper, IAddonRepositoryService addonRepository)
        {
            this.mapper = mapper;
            this.addonRepository = addonRepository;
        }

        public async Task<List<AddonDto>> Handle(GetAddonsFromRepositoryCommand request, CancellationToken cancellationToken)
        {
            var repoString = await addonRepository.GetRepository();

            JObject repo = JObject.Parse(repoString);
            if (repo == null || repo["repository"] == null)
            {
                throw new Exception("Repository is not valid");
            }

            var repoToken = repo["repository"];
            var repoItems = repoToken.ToObject<List<AddonDto>>();
            return repoItems;
        }
    }
}

