using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddons
{
    public class GetAddonsCommand : CommandBase<List<AddonDto>>
    {
        public bool Flat { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
    }
}
