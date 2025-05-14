using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery
{
    public class GetPropertiesByQueryCommand : CommandBase<List<PropertyDTO>>
    {
        public PlayerDTO? Player { get; set; }
        public Guid[]? ParentIDs { get; set; }
        public string[]? PropertyNames { get; set; }
        public Guid[]? Ids { get; set; }
        public Func<PropertyDTO, bool>? Filter { get; set; }
        public string Prefix { get; set; }
    }
}
