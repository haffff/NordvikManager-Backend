
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class GetGameListCommand : CommandBase<GetGameListCommandResponse>
    {
        public string UserId { get; set; }
        /// <summary>The server's local admin: every game, not only the user's.</summary>
        public bool AllGames { get; set; }
    }
}

