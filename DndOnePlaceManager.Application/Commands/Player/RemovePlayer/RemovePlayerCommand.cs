using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Player.RemovePlayer
{
    public class RemovePlayerCommand : CommandBase<CommandResponse>
    {
        public Guid GameID { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid PlayerID { get; set; }
    }
}
