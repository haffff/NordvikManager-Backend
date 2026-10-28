using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.Commands.Security.GetPlayerPermissions
{
    public class GetPlayerPermissionsCommand : GamePlayerCommandBase<Dictionary<Guid, Permission>>
    {

    }
}
