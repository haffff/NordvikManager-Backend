using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;

namespace NordvikManager.Application.Commands.Elements.GetElementDetails
{
    public class GetElementDetailsCommand : GamePlayerCommandBase<Dictionary<string, object>>
    {
        public Guid ElementId { get; set; }
        public string Name { get; set; }
    }
}
