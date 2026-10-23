using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    internal class GetServerSettingsCommandHandler : HandlerBase<GetServerSettingsCommand, ServerSettingsDTO>
    {
        private readonly ServerSettingsView settings;

        public GetServerSettingsCommandHandler(IDbContext dbContext, IMapper mapper, ServerSettingsView settings) : base(dbContext, mapper)
        {
            this.settings = settings;
        }

        public override Task<ServerSettingsDTO> Handle(GetServerSettingsCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(settings.ToDto());
    }
}
