using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Addons.InstallAddon
{
    public class InstallAddonCommand : GamePlayerCommandBase<(CommandResponse, Guid)>
    {
        public bool? AutoInstallDeps { get; set; }
        public byte[]? AddonFile { get; set; }
        public string? AddonFileName { get; set; }
        public string? AddonSourceKey { get; set; }
        public bool Reinstall { get; set; } = false;
    }
}
