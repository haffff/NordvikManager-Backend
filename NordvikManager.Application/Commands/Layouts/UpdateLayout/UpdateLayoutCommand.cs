using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.Commands.Layouts.UpdateLayout
{
    public class UpdateLayoutCommand : CommandBase<CommandResponse>
    {
        public LayoutDTO Dto { get; set; }
        public User user { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
