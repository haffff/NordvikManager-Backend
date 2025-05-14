using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Resources.GetResource
{
    public class GetResourceCommand : CommandBase<ResourceDTO>
    {
        public Guid? ResourceId { get; set; }
        public string? Key { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
