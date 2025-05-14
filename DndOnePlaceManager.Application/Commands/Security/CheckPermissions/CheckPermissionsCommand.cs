using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Security.CheckPermissions
{
    public class CheckPermissionsCommand : CommandBase<bool>
    {
        public PlayerDTO Player { get; set; }
        public Guid EntityId { get; set; }
        public Permission RequiredPermission { get; set; }
    }
}
