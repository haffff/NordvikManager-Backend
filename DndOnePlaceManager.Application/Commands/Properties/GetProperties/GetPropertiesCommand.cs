using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProperties
{
    public class GetPropertiesCommand : CommandBase<List<PropertyDTO>>
    {
        public PlayerDTO Player { get; set; }
        public Guid ParentID { get; set; }
        public string? PropertyName { get; set; }
        public Guid[] Ids { get; set; }
    }
}
