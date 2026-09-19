using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using System;

namespace DndOnePlaceManager.Application.Commands.Layouts.ForceLayout
{
    public class ForceLayoutCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameID { get; set; }
        public Guid LayoutId { get; set; }
    }
}
