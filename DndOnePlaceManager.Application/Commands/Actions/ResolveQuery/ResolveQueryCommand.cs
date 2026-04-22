using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;
using System.Collections.Generic;

namespace DndOnePlaceManager.Application.Commands.Actions.ResolveQuery
{
    public class ResolveQueryCommand : CommandBase<string>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Expression { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new();
    }
}
