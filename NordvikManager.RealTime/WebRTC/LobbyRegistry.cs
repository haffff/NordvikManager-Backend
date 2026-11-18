using NordvikManager.Services.Implementations;
using System;
using System.Collections.Concurrent;

namespace NordvikManager.WebRTC
{
    public class LobbyRegistry : ILobbyRegistry
    {
        public ConcurrentDictionary<Guid, GameLobby> Games { get; } = new ConcurrentDictionary<Guid, GameLobby>();
    }
}
