using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Abstracts a single transport connection to a player — either a WebSocket or a WebRTC data channel.
    /// </summary>
    public interface IPlayerConnection
    {
        Task<bool> SendMessageToPlayer(object message);
    }
}
