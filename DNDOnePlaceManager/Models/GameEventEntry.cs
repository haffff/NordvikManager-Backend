using System;

namespace DNDOnePlaceManager.Models
{
    /// <summary>
    /// A single entry in the per-game GM event log.
    /// </summary>
    public class GameEventEntry
    {
        public Guid   Id        { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>"Info", "Warning", or "Error".</summary>
        public string Level    { get; init; } = "Info";

        /// <summary>High-level area: "Addon", "Action", "Permission", "Command", "System", "API".</summary>
        public string Category { get; init; } = "System";

        public string Message  { get; init; } = string.Empty;

        /// <summary>Display name of the player that triggered the event, if known.</summary>
        public string? Player  { get; init; }

        /// <summary>Optional JSON string with extra diagnostic context.</summary>
        public string? Details { get; init; }
    }
}
