using DndOnePlaceManager.Domain.Enums;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class MapDTO : IGameDataTransferObject
    {
        public string? Name { get; set; }
        public string? Path { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }
        public Guid? GameID { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public IEnumerable<ElementDTO>? Elements { get; set; }
        public int? GridSize { get; set; }
        public bool? GridVisible { get; set; }
        public Permission? Permission { get; set; }
        public int? GridUnitSize { get; set; }
        public IEnumerable<PropertyDTO>? Properties { get; set; }
    }
}
