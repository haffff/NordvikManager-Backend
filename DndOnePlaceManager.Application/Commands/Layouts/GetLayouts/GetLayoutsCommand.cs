using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.Layouts.GetLayouts
{
    public class GetLayoutsCommand : CommandBase<List<LayoutDTO>>
    {
        public Guid GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public bool Flat { get; set; }
    }
}
