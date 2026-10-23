using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Sandbox;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    internal class SetSandboxAllowedOriginsCommandHandler : HandlerBase<SetSandboxAllowedOriginsCommand, ServerSettingsDTO>
    {
        private readonly ServerSettingsView settings;

        public SetSandboxAllowedOriginsCommandHandler(IDbContext dbContext, IMapper mapper, ServerSettingsView settings) : base(dbContext, mapper)
        {
            this.settings = settings;
        }

        public override Task<ServerSettingsDTO> Handle(SetSandboxAllowedOriginsCommand request, CancellationToken cancellationToken)
        {
            settings.ThrowIfLocked();
            // These go into the card sandbox CSP as they are: anything but a bare https origin is refused.
            var origins = ServerSettingsView.CleanList(request.Origins, SandboxOrigins.Invalid,
                "an https origin (e.g. https://cdn.example.com or https://*.example.com, without a path)");

            // The store's Changed event updates SandboxOrigins; the sandbox CSP follows on the next request.
            settings.Store.Update(s => s.Security.SandboxAllowedOrigins = origins);
            return Task.FromResult(settings.ToDto());
        }
    }
}
