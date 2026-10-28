using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NordvikManager.Enums
{
    /// <summary>
    /// An action's hook filter (ActionModel.HookFilter): which occurrences of its hook run it.
    /// What the filter means depends on the hook; this class is the one place that knows.
    ///
    /// | Hook                | Hook filter                                                        |
    /// |---------------------|--------------------------------------------------------------------|
    /// | 19 Chat Command     | Required. The commands the action handles: names without "/" (a    |
    /// |                     | leading "/" is dropped), separated by commas or spaces, e.g.       |
    /// |                     | "poll, vote". 1-32 characters of a-z 0-9 _ - each, case-insensitive,|
    /// |                     | at most 20, none of the built-in commands (r roll gr w gm help).    |
    /// |                     | Only a listed command is claimed from chat and runs the action;    |
    /// |                     | any other "/name" still gets "Wrong command".                      |
    /// | every other hook    | No filter yet: it must be empty.                                   |
    ///
    /// A new filter for another hook goes here (Normalize and Accepts), in its HookMeta
    /// (Filter, FilterRequired) and in the docs' hook filter table (addon-guide/actions/hooks.md,
    /// static/ai/ACTIONS.md) and the action schema (NordvikManager-Addons schemas/action.json).
    /// </summary>
    public static class HookFilters
    {
        public const int MaxChatCommands = 20;
        public const int MaxChatCommandLength = 32;

        /// <summary>Chat commands the chat handles itself (without "/"); they never reach actions.</summary>
        public static readonly IReadOnlySet<string> BuiltInChatCommands =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "r", "roll", "gr", "w", "gm", "help" };

        private static readonly Regex ChatCommandName = new("^[a-z0-9_-]{1," + MaxChatCommandLength + "}$", RegexOptions.Compiled);

        /// <summary>
        /// Checks an action's filter for its hook and tidies it for storing. Returns the value to
        /// store (null for none) or, when the filter doesn't fit the hook, an error to show the
        /// action's author.
        /// </summary>
        public static (string? Value, string? Error) Normalize(Hook hook, string? filter)
        {
            var text = filter?.Trim() ?? string.Empty;
            if (hook != Hook.ChatCommand)
                return text.Length == 0
                    ? (null, null)
                    : (null, $"The {hook} hook takes no hook filter; leave it empty.");

            var names = new List<string>();
            foreach (var part in text.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = (part.StartsWith('/') ? part[1..] : part).ToLowerInvariant();
                if (!ChatCommandName.IsMatch(name))
                    return (null, $"'{part}' isn't a chat command name: up to {MaxChatCommandLength} letters a-z, digits, '_' or '-', without spaces.");
                if (BuiltInChatCommands.Contains(name))
                    return (null, $"/{name} is built in: the chat handles it, so an action never gets it.");
                if (!names.Contains(name))
                    names.Add(name);
            }

            if (names.Count == 0)
                return (null, "A Chat Command action needs the commands it handles in its hook filter, e.g. poll, vote.");
            if (names.Count > MaxChatCommands)
                return (null, $"A Chat Command action can handle at most {MaxChatCommands} commands.");
            return (string.Join(", ", names), null);
        }

        /// <summary>
        /// Whether an occurrence of the hook runs an action with this filter. <paramref name="key"/>
        /// is what the hook is filtered by: the command name (without "/") for Chat Command.
        /// Hooks without a filter accept everything.
        /// </summary>
        public static bool Accepts(Hook hook, string? filter, string? key)
        {
            if (hook != Hook.ChatCommand)
                return true;
            if (string.IsNullOrWhiteSpace(filter) || string.IsNullOrWhiteSpace(key))
                return false;
            var wanted = key.Trim().TrimStart('/');
            return filter.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));
        }
    }
}
