using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Properties
{
    public class UpdatePropertyCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public PropertyDTO Property { get; set; }
    }
}
