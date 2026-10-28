using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.Addons.GetAddons
{
    public class GetAddonsCommand : CommandBase<List<AddonDto>>
    {
        public bool Flat { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
    }
}
