using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled
{
    public class SetAddonEnabledCommand : GamePlayerCommandBase<CommandResponse>
    {
        /// <summary>The addon id or key.</summary>
        public string? AddonId { get; set; }
        public bool Enabled { get; set; }
    }
}
