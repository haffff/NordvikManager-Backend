using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest
{
    /// <summary>
    /// One implementation per real controller domain (BattleMap, Properties, ...), mirroring
    /// IWebSocketHandler's shape: switches on the incoming request, returns null for anything
    /// it doesn't recognize so WebRTCRestDispatcher can try the next handler.
    /// </summary>
    public interface IWebRTCRestHandler
    {
        Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId);
    }
}
