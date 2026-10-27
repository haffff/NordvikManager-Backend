using System;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Resources
{
    public class AddPropertyCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public PropertyDTO Property { get; set; }
    }
}
