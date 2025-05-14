using DNDOnePlaceManager.WebSockets;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Extensions
{
    public static class WebSocketExtensions
    {
        public async static Task<bool> SendObject(this WebSocket ws, object obj)
        {
            return await SendText(ws, JsonConvert.SerializeObject(obj));
        }

        public async static Task<bool> SendText(this WebSocket ws, string str)
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(str), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            return true;
        }

        public static async Task SendMessageToPlayer(this List<WebSocketManager> wsList, object message)
        {
            foreach (var ws in wsList)
            {
                await ws.SendMessageToPlayer(message);
            }
        }
    }
}
