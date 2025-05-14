using DndOnePlaceManager.Domain.Enums;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class ElementDTO : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string? Object { get; set; }
        public Guid? MapID { get; set; }
        public IEnumerable<PropertyDTO>? Properties { get; set; }
        public bool? Visible { get; set; }
        public bool? IsPublic { get; set; }
        public bool? Selectable { get; set; }
        public int? Layer { get; set; }
        public Permission? Permission { get; set; }
    }
}
