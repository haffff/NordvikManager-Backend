using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    internal class SetProxyAllowedPrivateHostsCommandHandler : HandlerBase<SetProxyAllowedPrivateHostsCommand, ServerSettingsDTO>
    {
        public const int MaxEntries = 50;

        private readonly IServerSettingsStore store;
        private readonly ServerSettingsOptions options;
        private readonly ProxyOptions proxyOptions;

        public SetProxyAllowedPrivateHostsCommandHandler(IDbContext dbContext, IMapper mapper, IServerSettingsStore store, ServerSettingsOptions options, ProxyOptions proxyOptions) : base(dbContext, mapper)
        {
            this.store = store;
            this.options = options;
            this.proxyOptions = proxyOptions;
        }

        public override Task<ServerSettingsDTO> Handle(SetProxyAllowedPrivateHostsCommand request, CancellationToken cancellationToken)
        {
            if (!options.AllowRuntimeChanges)
                throw new ServerSettingsLockedException();

            var hosts = (request.Hosts ?? new List<string>())
                .Select(h => h?.Trim() ?? "")
                .Where(h => h != "")
                .DistinctBy(h => h.ToLowerInvariant())
                .ToList();

            var invalid = ProxyOptions.Invalid(hosts);
            if (invalid.Count > 0)
                throw new ServerSettingsException($"Not a host or host:port: {string.Join(", ", invalid)}. Use e.g. localhost:8080 or trilium.lan, without http:// or a path.");
            if (hosts.Count > MaxEntries)
                throw new ServerSettingsException($"At most {MaxEntries} private hosts can be allowed.");

            // The store's Changed event updates ProxyOptions, so the next request already sees this.
            store.Update(s => s.Security.ProxyAllowedPrivateHosts = hosts);

            return Task.FromResult(ServerSettingsDTO.From(store, options, proxyOptions));
        }
    }
}
