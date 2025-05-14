using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Addons.UninstallAddon
{
    public class UninstallAddonCommand : GamePlayerCommandBase<CommandResponse>
    {
        public Guid? AddonId { get; set; }
        //public bool? DeleteResources { get; set; }
        //public bool? DeleteActions { get; set; }
        //public bool? DeleteTemplates { get; set; }
        //public bool? DeleteViews { get; set; }
    }
}
