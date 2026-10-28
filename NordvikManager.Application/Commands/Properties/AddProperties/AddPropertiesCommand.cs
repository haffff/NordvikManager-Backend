using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Properties.AddProperties
{
    public class AddPropertiesCommand : GamePlayerCommandBase<CommandResponse>
    {
        public PropertyDTO[] Properties { get; set; }
    }
}
