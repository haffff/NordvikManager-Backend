using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.Commands.Security.GetPermissions
{
    public class GetPermissionsCommand : CommandBase<Dictionary<Guid, Permission>>
    {
        public Guid EntityId { get; set; }
        public PlayerDTO Player { get; set; }
        public PlayerDTO TargetPlayer { get; set; }
    }
}
