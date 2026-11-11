using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Addons.UninstallAddon
{
    public class UninstallAddonCommand : GamePlayerCommandBase<(CommandResponse, UninstallAddonCommandResponse)>
    {
        public Guid? AddonId { get; set; }
        public string? AddonKey { get; set; }

        /// <summary>
        /// Set by an update, which removes the old version itself and installs the new one at
        /// once: addons that depend on this one don't block it then.
        /// </summary>
        public bool ForReinstall { get; set; }
    }
}
