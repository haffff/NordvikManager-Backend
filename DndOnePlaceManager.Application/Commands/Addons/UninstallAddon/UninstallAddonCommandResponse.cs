namespace DndOnePlaceManager.Application.Commands.Addons.UninstallAddon
{
    public class UninstallAddonCommandResponse
    {
        public UninstallAddonCommandResponse()
        {
        }

        public Guid? AddonId { get; set; }
        public string? AddonKey { get; set; }
        public string? AddonName { get; set; }
        public string? AddonVersion { get; set; }
    }
}
