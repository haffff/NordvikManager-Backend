#nullable enable
using System;

namespace DndOnePlaceManager.Application.Interfaces
{
    /// <summary>
    /// Per-game structured event logger available to all application-layer handlers.
    /// Each active <c>GameLobby</c> wires this service to its own in-memory
    /// <c>GameEventLog</c> so that logs from command handlers are captured
    /// alongside the lobby's own entries and can be surfaced to the GM.
    ///
    /// All methods are intentionally fire-and-forget (no return value) —
    /// logging must never affect command execution.
    /// </summary>
    public interface IGameEventLogger
    {
        /// <summary>Logs an informational entry.</summary>
        void Info(string category, string message, string? player = null, object? details = null);

        /// <summary>Logs a warning entry.</summary>
        void Warning(string category, string message, string? player = null, object? details = null);

        /// <summary>Logs an error entry, optionally with an exception.</summary>
        void Error(string category, string message, string? player = null, object? details = null);

        /// <summary>Logs an exception as an error, capturing type and message in details.</summary>
        void Exception(string category, Exception exception, string? contextMessage = null, string? player = null);
    }
}
