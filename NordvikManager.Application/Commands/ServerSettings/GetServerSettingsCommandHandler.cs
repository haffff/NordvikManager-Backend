using NordvikManager.Application.DataTransferObjects.ServerSettings;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.ServerSettings
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
