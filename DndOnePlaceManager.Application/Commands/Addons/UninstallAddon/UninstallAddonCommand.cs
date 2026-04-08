using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Addons.UninstallAddon
{
    public class UninstallAddonCommand : GamePlayerCommandBase<CommandResponse>
    {
        public Guid? AddonId { get; set; }
        public string? AddonKey { get; set; }
    }
}
