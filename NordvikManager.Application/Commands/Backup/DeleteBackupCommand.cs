namespace NordvikManager.Application.Commands.Backup
{
    public class DeleteBackupCommand : CommandBase<bool>
    {
        public string Name { get; set; } = "";
    }
}
