using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class GameItemDTO
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Image { get; set; }
        public string? LongDescription { get; internal set; }
        public string? ShortDescription { get; internal set; }
        public string? Color { get; internal set; }
        public bool IsOwner { get; internal set; }
    }
}
