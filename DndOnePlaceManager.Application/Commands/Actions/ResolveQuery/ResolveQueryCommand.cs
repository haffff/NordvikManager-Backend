using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;
using System.Collections.Generic;

namespace DndOnePlaceManager.Application.Commands.Actions.ResolveQuery
{
    public class ResolveQueryCommand : CommandBase<ResolveQueryResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Expression { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new();
    }

    /// <summary>The filled-in expression, and the tokens that came out empty because nothing was found.</summary>
    public class ResolveQueryResult
    {
        public string Result { get; set; }
        public IReadOnlyList<string> Unresolved { get; set; } = Array.Empty<string>();
    }
}
