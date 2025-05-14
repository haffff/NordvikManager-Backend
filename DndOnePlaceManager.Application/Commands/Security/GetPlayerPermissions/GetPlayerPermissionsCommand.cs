using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Security.GetPlayerPermissions
{
    public class GetPlayerPermissionsCommand : GamePlayerCommandBase<Dictionary<Guid, Permission>>
    {

    }
}
