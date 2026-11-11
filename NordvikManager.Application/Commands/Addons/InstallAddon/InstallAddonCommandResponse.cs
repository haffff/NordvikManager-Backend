namespace NordvikManager.Application.Commands.Addons.InstallAddon
{
    public class InstallAddonCommandResponse
    {
        public InstallAddonCommandResponse()
        {
        }

        public Guid? AddonId { get; set; }
        public string? AddonKey { get; set; }
        public string? AddonName { get; set; }
        public string? AddonVersion { get; set; }

        /// <summary>The dependencies installed or updated with it, deepest first: their Install hooks run before its own.</summary>
        public List<InstallAddonCommandResponse> InstalledDependencies { get; set; } = new();
    }
}