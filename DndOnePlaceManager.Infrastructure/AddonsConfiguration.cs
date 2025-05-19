namespace DndOnePlaceManager.Infrastructure
{
    internal class AddonsConfiguration
    {
        public string MainRepository { get; set; } = string.Empty;
        public string[] TrustedRepositories { get; set; } = Array.Empty<string>();
        public bool CanAccessNotAllowedRepository { get; set; } = false;
    }
}