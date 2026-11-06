using NordvikManager.Application.DataTransferObjects.ServerSettings;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Application.Commands.ServerSettings
{
    internal class SetUploadLimitsCommandHandler : HandlerBase<SetUploadLimitsCommand, ServerSettingsDTO>
    {
        /// <summary>10 TB: more is a typo.</summary>
        public const long MaxLimitMb = 10_000_000;
        private readonly ServerSettingsView settings;

        public SetUploadLimitsCommandHandler(IDbContext dbContext, IMapper mapper, ServerSettingsView settings) : base(dbContext, mapper)
        {
            this.settings = settings;
        }

        public override Task<ServerSettingsDTO> Handle(SetUploadLimitsCommand request, CancellationToken cancellationToken)
        {
            settings.ThrowIfLocked();
            foreach (var mb in new[] { request.GmGameLimitMb, request.PlayerLimitMb })
                if (mb < 0 || mb > MaxLimitMb)
                    throw new ServerSettingsException($"An upload limit is a number of MB from 0 (no limit) to {MaxLimitMb}.");

            // read on every upload (UploadQuota), so it applies at once
            settings.Store.Update(s =>
            {
                s.Uploads.GmGameLimitMb = request.GmGameLimitMb;
                s.Uploads.PlayerLimitMb = request.PlayerLimitMb;
            });
            return Task.FromResult(settings.ToDto());
        }
    }
}
