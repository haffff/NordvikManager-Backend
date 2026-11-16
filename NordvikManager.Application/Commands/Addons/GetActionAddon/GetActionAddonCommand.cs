using NordvikManager.Application.Commands;

namespace NordvikManager.Application.Commands.Addons.GetActionAddon
{
    /// <summary>
    /// The key of the addon an action came with; null for an action the GM made (or none).
    /// For the action engine, which runs as the game's system: no player permission check.
    /// </summary>
    public class GetActionAddonCommand : CommandBase<string?>
    {
        public Guid ActionId { get; set; }
    }
}
