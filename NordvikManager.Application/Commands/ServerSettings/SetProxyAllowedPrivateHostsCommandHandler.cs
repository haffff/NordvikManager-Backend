using NordvikManager.Application.DataTransferObjects.ServerSettings;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Proxy;

namespace NordvikManager.Application.Commands.ServerSettings
{
    internal class SetProxyAllowedPrivateHostsCommandHandler : HandlerBase<SetProxyAllowedPrivateHostsCommand, ServerSettingsDTO>
    {
        private readonly ServerSettingsView settings;

        public SetProxyAllowedPrivateHostsCommandHandler(IDbContext dbContext, IMapper mapper, ServerSettingsView settings) : base(dbContext, mapper)
        {
            this.settings = settings;
        }

        public override Task<ServerSettingsDTO> Handle(SetProxyAllowedPrivateHostsCommand request, CancellationToken cancellationToken)
        {
            settings.ThrowIfLocked();
            var hosts = ServerSettingsView.CleanList(request.Hosts, ProxyOptions.Invalid,
                "a host or host:port (e.g. localhost:8080 or trilium.lan, without http:// or a path)");

            // The store's Changed event updates ProxyOptions, so the next request already sees this.
            settings.Store.Update(s => s.Security.ProxyAllowedPrivateHosts = hosts);
            return Task.FromResult(settings.ToDto());
        }
    }
}
