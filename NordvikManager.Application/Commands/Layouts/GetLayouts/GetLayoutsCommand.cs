using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.Layouts.GetLayouts
{
    public class GetLayoutsCommand : CommandBase<List<LayoutDTO>>
    {
        public Guid GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public bool Flat { get; set; }
    }
}
