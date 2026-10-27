using NordvikManager.WebRTC;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Extensions
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
