using System;

namespace NordvikManager.Application.Commands.Game.GetGameCentralSessionId
{
    public class GetGameCentralSessionIdCommand : CommandBase<string?>
    {
        public Guid GameID { get; set; }
    }
}
