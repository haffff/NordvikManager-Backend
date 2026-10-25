using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Newtonsoft.Json.Linq;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddonCategories
{
    /// <summary>The addon registry's categories (addons.json "categories"), for filtering in the Addons panel.</summary>
    public class GetAddonCategoriesCommand : CommandBase<List<RegistryCategoryDto>>
    {
    }

    internal class GetAddonCategoriesCommandHandler : IRequestHandler<GetAddonCategoriesCommand, List<RegistryCategoryDto>>
    {
        private readonly IAddonRepositoryService addonRepository;

        public GetAddonCategoriesCommandHandler(IAddonRepositoryService addonRepository)
        {
            this.addonRepository = addonRepository;
        }

        public async Task<List<RegistryCategoryDto>> Handle(GetAddonCategoriesCommand request, CancellationToken cancellationToken)
        {
            var repo = JObject.Parse(await addonRepository.GetRepository());
            return repo["categories"]?.ToObject<List<RegistryCategoryDto>>() ?? new List<RegistryCategoryDto>();
        }
    }
}
