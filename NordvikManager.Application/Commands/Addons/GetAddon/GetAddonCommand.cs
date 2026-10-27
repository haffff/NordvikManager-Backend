using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;

namespace NordvikManager.Application.Commands.Addons.GetAddon
{
    public class GetAddonCommand : GenericGetCommand<AddonDto>
    {
        public string AddonKey { get; set; } = string.Empty;
    }
}
