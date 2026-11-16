using System;
using System.Collections.Generic;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Which action is running, as the engine (ActionProcessingService) knows it: in every run's
    /// variables under <see cref="Key"/>. Steps decide by it whose rules apply, e.g. which addon's
    /// network holds Send Request and Download Resource. Steps write strings, numbers and JSON
    /// into variables and can't make one of these, so overwriting "actionId" (any step's Output can)
    /// or this key doesn't make an action someone else: overwritten, there is no context, and the
    /// steps that need one refuse.
    /// </summary>
    public sealed class ActionRunContext
    {
        public const string Key = "__actionRun";

        public ActionRunContext(Guid actionId, string? actionName)
        {
            ActionId = actionId;
            ActionName = actionName;
        }

        public Guid ActionId { get; }
        public string? ActionName { get; }

        /// <summary>The engine's context in these variables, or null (none, or something else under its key).</summary>
        public static ActionRunContext? Of(Dictionary<string, object> variables) =>
            variables.TryGetValue(Key, out var value) ? value as ActionRunContext : null;
    }
}
