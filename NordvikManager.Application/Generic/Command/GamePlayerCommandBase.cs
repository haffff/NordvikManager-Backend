using NordvikManager.Application.Commands;
using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Generic.Command
{
    public class GamePlayerCommandBase<TResponse> : CommandBase<TResponse>
    {
        public Guid GameID { get; set; }
        public PlayerDTO? Player { get; set; }
    }
}
