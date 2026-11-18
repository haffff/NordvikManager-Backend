using NordvikManager.Services.Implementations;
using System;
using System.Collections.Concurrent;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Singleton registry of active game lobbies, shared between WebSocket and WebRTC paths.
    /// </summary>
    public interface ILobbyRegistry
    {
        ConcurrentDictionary<Guid, GameLobby> Games { get; }
    }
}
