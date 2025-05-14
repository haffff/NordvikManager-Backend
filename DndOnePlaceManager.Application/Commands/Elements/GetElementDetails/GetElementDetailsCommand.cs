using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Command;

namespace DndOnePlaceManager.Application.Commands.Elements.GetElementDetails
{
    public class GetElementDetailsCommand : GamePlayerCommandBase<Dictionary<string,object>>
    {
        public Guid ElementId { get; set; }
        public string Name { get; set; }
    }
}
