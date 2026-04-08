using DNDOnePlaceManager.Services.Implementations;
using System;
using System.Collections.Concurrent;

namespace DNDOnePlaceManager.WebRTC
{
    public class LobbyRegistry : ILobbyRegistry
    {
        public ConcurrentDictionary<Guid, GameLobby> Games { get; } = new ConcurrentDictionary<Guid, GameLobby>();
    }
}
