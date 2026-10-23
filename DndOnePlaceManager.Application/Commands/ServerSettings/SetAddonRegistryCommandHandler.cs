using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    internal class SetAddonRegistryCommandHandler : HandlerBase<SetAddonRegistryCommand, ServerSettingsDTO>
    {
        private readonly ServerSettingsView settings;

        public SetAddonRegistryCommandHandler(IDbContext dbContext, IMapper mapper, ServerSettingsView settings) : base(dbContext, mapper)
        {
            this.settings = settings;
        }

        public override Task<ServerSettingsDTO> Handle(SetAddonRegistryCommand request, CancellationToken cancellationToken)
        {
            settings.ThrowIfLocked();

            var repository = string.IsNullOrWhiteSpace(request.MainRepository) ? null : request.MainRepository.Trim();
            if (repository != null && !AddonRegistryOptions.IsValidRepositoryUrl(repository))
                throw new ServerSettingsException($"Not an https URL: {repository}. The addon registry must be served over https, e.g. https://raw.githubusercontent.com/.../addons.json.");

            var featured = request.FeaturedAddons == null
                ? null
                : ServerSettingsView.CleanList(request.FeaturedAddons, keys => keys.Where(k => !AddonRegistryOptions.IsValidAddonKey(k)).Select(k => k ?? "").ToList(),
                    "an addon key (e.g. dnd5e)");

            // The store's Changed event updates AddonRegistryOptions for the lobby and game creation.
            settings.Store.Update(s =>
            {
                s.Addons.MainRepository = repository;
                s.Addons.FeaturedAddons = featured;
            });
            return Task.FromResult(settings.ToDto());
        }
    }
}
