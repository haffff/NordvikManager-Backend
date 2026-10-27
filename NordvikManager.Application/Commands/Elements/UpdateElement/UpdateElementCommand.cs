
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Elements
{
    public class UpdateElementCommand : CommandBase<CommandResponse>
    {
        public ElementDTO Element { get; set; }
        public PlayerDTO Player { get; set; }
    }
}

