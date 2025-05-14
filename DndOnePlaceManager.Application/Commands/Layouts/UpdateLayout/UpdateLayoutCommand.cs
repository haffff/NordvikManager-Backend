using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Layouts.UpdateLayout
{
    public class UpdateLayoutCommand : CommandBase<CommandResponse>
    {
        public LayoutDTO Dto { get; set; }
        public User user { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
