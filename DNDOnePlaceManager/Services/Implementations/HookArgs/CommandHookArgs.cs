using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebSockets;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Services.Implementations.HookArgs
{
    public class CommandHookArgs : HookArgs
    {
        public WebSocketCommand Command { get; set; }
        public JObject Data { get; set; }
        public PlayerDTO Player { get; set; }
    }
}
