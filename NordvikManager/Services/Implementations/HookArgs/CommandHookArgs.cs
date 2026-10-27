using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.WebSockets;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Services.Implementations.HookArgs
{
    public class CommandHookArgs : HookArgs
    {
        public WebSocketCommand Command { get; set; }
        public JObject Data { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
