using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Enums;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services
{
    /// <summary>Runs Hook.Install for what an install added to a running game.</summary>
    public static class AddonInstallHooks
    {
        /// <summary>
        /// Fires Hook.Install for each dependency the install added (deepest first), then for
        /// the addon itself, so an addon's install action can rely on what its dependencies set
        /// up. <paramref name="markFired"/> records each one, so the catch-up on the next player
        /// join doesn't run it again.
        /// </summary>
        public static async Task FireAsync(IActionProcessingService actions, Guid gameId, Guid playerId, InstallAddonCommandResponse installed, Func<Guid, Task> markFired)
        {
            foreach (var addon in installed.InstalledDependencies.Append(installed))
            {
                await actions.CallHookAsync(Hook.Install, new AddonHookArgs
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    AddonKey = addon.AddonKey,
                });
                if (addon.AddonId is Guid id)
                    await markFired(id);
            }
        }
    }
}
