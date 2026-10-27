using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.Commands.Player.RemovePlayer
{
    public class RemovePlayerCommand : CommandBase<CommandResponse>
    {
        public Guid GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlayerID { get; set; }
    }
}
