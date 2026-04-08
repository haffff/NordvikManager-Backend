using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProtectedProperties
{
    public class GetProtectedPropertiesCommand : CommandBase<List<PropertyDTO>>
    {
        public Guid? ParentID { get; set; }
        public string[]? PropertyNames { get; set; }
        public Guid[]? Ids { get; set; }
        public string? Prefix { get; set; }
    }
}
