using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using System;

namespace NordvikManager.Application.Commands.Layouts.ForceLayout
{
    public class ForceLayoutCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameID { get; set; }
        public Guid LayoutId { get; set; }
    }
}
