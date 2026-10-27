namespace NordvikManager.Application.Commands.Player.CheckUserBanned
{
    public class CheckUserBannedCommand : CommandBase<bool>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}
