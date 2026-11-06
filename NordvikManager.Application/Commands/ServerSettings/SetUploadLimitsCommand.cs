using NordvikManager.Application.DataTransferObjects.ServerSettings;

namespace NordvikManager.Application.Commands.ServerSettings
{
    /// <summary>How much may be uploaded into one game, in MB (0 = no limit): by its GMs together, and by each other player.</summary>
    public class SetUploadLimitsCommand : CommandBase<ServerSettingsDTO>
    {
        public long GmGameLimitMb { get; set; }

        public long PlayerLimitMb { get; set; }
    }
}
