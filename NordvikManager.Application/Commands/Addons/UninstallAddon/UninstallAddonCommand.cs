using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Addons.UninstallAddon
{
    public class UninstallAddonCommand : GamePlayerCommandBase<(CommandResponse, UninstallAddonCommandResponse)>
    {
        public Guid? AddonId { get; set; }
        public string? AddonKey { get; set; }
    }
}
