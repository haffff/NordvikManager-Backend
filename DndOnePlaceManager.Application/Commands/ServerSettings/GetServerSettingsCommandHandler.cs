using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    internal class GetServerSettingsCommandHandler : HandlerBase<GetServerSettingsCommand, ServerSettingsDTO>
    {
        private readonly IServerSettingsStore store;
        private readonly ServerSettingsOptions options;
        private readonly ProxyOptions proxyOptions;

        public GetServerSettingsCommandHandler(IDbContext dbContext, IMapper mapper, IServerSettingsStore store, ServerSettingsOptions options, ProxyOptions proxyOptions) : base(dbContext, mapper)
        {
            this.store = store;
            this.options = options;
            this.proxyOptions = proxyOptions;
        }

        public override Task<ServerSettingsDTO> Handle(GetServerSettingsCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(ServerSettingsDTO.From(store, options, proxyOptions));
    }
}
