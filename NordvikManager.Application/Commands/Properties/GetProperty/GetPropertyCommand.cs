using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Properties.GetProperty
{
    public class GetPropertyCommand : CommandBase<(CommandResponse, PropertyDTO)>
    {
        public PlayerDTO Player { get; set; }
        public Guid? Id { get; set; }
        public Guid? ParentID { get; set; }
        public string Name { get; set; }
    }
}
