using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace NordvikManager.Wasm.Host
{
    /// <summary>
    /// Newly-written (not ported) equivalent of what SessionController.cs does on
    /// session start/stop — that controller is an ASP.NET Core action and isn't part
    /// of this extraction. GameLobby itself is never DI-registered in the real app
    /// either (see SessionController.cs:107-115); it's always manually `new`'d against
    /// an IServiceScopeFactory and tracked in ILobbyRegistry.Games.
    ///
    /// Fixes a bug found while reading the original: SessionController.cs:136 removes
    /// a lobby from the registry on session stop but never calls .Dispose() on it,
    /// leaking the IServiceScope GameLobby's constructor creates. That leak matters
    /// more in a single long-lived wasm process than a per-request Kestrel host, so
    /// StopSession here calls Dispose() where the original didn't.
    /// </summary>
    public static class SessionGlue
    {
        public static GameLobby StartSession(IServiceProvider provider, Guid gameId, PlayerDTO? systemPlayer = null)
        {
            var registry = provider.GetRequiredService<ILobbyRegistry>();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            return registry.Games.GetOrAdd(gameId, _ =>
            {
                var lobby = new GameLobby(scopeFactory) { GameId = gameId };
                if (systemPlayer != null)
                    lobby.SystemPlayer = systemPlayer;
                return lobby;
            });
        }

        public static void StopSession(IServiceProvider provider, Guid gameId)
        {
            var registry = provider.GetRequiredService<ILobbyRegistry>();
            if (registry.Games.TryRemove(gameId, out var lobby))
                lobby.Dispose();
        }
    }
}
