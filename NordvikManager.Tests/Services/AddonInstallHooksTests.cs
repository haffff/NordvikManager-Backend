using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Enums;
using NordvikManager.Services;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Services.Interfaces;
using Moq;

namespace NordvikManager.Tests.Services
{
    // An addon's Install action may rely on what its dependencies' Install actions set up.
    // Dependencies installed with it used to wait for the next player to join instead.
    public class AddonInstallHooksTests
    {
        private static InstallAddonCommandResponse Installed(string key, params InstallAddonCommandResponse[] dependencies) => new()
        {
            AddonId = Guid.NewGuid(),
            AddonKey = key,
            InstalledDependencies = dependencies.ToList(),
        };

        [Fact]
        public async Task FireAsync_RunsDependenciesFirst_AndMarksEachFired()
        {
            var actions = new Mock<IActionProcessingService>();
            var order = new List<string?>();
            actions.Setup(a => a.CallHookAsync(Hook.Install, It.IsAny<HookArgs>()))
                .Callback((Hook _, HookArgs args) => order.Add(((AddonHookArgs)args).AddonKey))
                .Returns(Task.CompletedTask);
            var marked = new List<Guid>();
            var result = Installed("a", Installed("c"), Installed("b"));
            var gameId = Guid.NewGuid();
            var playerId = Guid.NewGuid();

            await AddonInstallHooks.FireAsync(actions.Object, gameId, playerId, result, id => { marked.Add(id); return Task.CompletedTask; });

            Assert.Equal(new[] { "c", "b", "a" }, order);
            Assert.Equal(new[] { result.InstalledDependencies[0].AddonId!.Value, result.InstalledDependencies[1].AddonId!.Value, result.AddonId!.Value }, marked);
            actions.Verify(a => a.CallHookAsync(Hook.Install, It.Is<HookArgs>(h => ((AddonHookArgs)h).GameId == gameId && ((AddonHookArgs)h).PlayerId == playerId)), Times.Exactly(3));
        }
    }
}
