using DNDOnePlaceManager.WebRTC;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Extensions
{
    public static class WebSocketExtensions
    {
        public static async Task SendMessageToPlayer(this List<IPlayerConnection> connections, object message)
        {
            foreach (var conn in connections)
            {
                await conn.SendMessageToPlayer(message);
            }
        }
    }
}
