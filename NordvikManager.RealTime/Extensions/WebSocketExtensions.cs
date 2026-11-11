using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Extensions
{
    public static class WebSocketExtensions
    {
        /// <summary>
        /// Sends to all the connections and never fails, so it is safe not to await. Every
        /// send starts before this returns, in order: a message goes out as it is now, even
        /// if the caller changes it for the next recipient, and one connection that is slow
        /// or broken doesn't hold up the others.
        /// </summary>
        public static Task SendMessageToPlayer(this IEnumerable<IPlayerConnection> connections, object message) =>
            Task.WhenAll(connections.Select(c => SendAsync(c, message)).ToList());

        private static async Task SendAsync(IPlayerConnection connection, object message)
        {
            try
            {
                await connection.SendMessageToPlayer(message);
            }
            catch (Exception)
            {
                // Connections report their own failures; this one going wrong is no reason
                // for the rest of the broadcast to stop, or for the caller to fail.
            }
        }
    }
}
