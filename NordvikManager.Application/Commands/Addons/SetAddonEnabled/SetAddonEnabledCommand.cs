using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Addons.SetAddonEnabled
{
    public class SetAddonEnabledCommand : GamePlayerCommandBase<CommandResponse>
    {
        /// <summary>The addon id or key.</summary>
        public string? AddonId { get; set; }
        public bool Enabled { get; set; }
    }
}
