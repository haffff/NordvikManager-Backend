namespace DndOnePlaceManager.Application.Commands.Addons.InstallAddon
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
    }
}