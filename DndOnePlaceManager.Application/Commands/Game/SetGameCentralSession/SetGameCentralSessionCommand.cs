using System;

namespace DndOnePlaceManager.Application.Commands.Game.SetGameCentralSession
{
    public class SetGameCentralSessionCommand : CommandBase<bool>
    {
        public Guid GameId { get; set; }
        public string CentralSessionId { get; set; } = string.Empty;
    }
}
