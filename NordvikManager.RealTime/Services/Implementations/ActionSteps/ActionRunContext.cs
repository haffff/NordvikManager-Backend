using System;
using System.Collections.Generic;
using System.Linq;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Which action is running, as the engine (ActionProcessingService) knows it: in every run's
    /// variables under <see cref="Key"/>. Steps decide by it whose rules apply, e.g. which addon's
    /// network holds Send Request and Download Resource. Steps write strings, numbers and JSON
    /// into variables and can't make one of these, so overwriting "actionId" (any step's Output can)
    /// or this key doesn't make an action someone else: overwritten, there is no context, and the
    /// steps that need one refuse.
    /// An action run by another (Execute Action, If/ForEach sub-actions) knows who called it
    /// (<see cref="CallerActionIds"/>): it gets the caller's variables, so a request it makes
    /// may be the caller's, and is held to the caller's addon's network too.
    /// </summary>
    public sealed class ActionRunContext
    {
        public const string Key = "__actionRun";

        public ActionRunContext(Guid actionId, string? actionName, ActionRunContext? caller = null)
        {
            ActionId = actionId;
            ActionName = actionName;
            CallerActionIds = caller == null
                ? Array.Empty<Guid>()
                : caller.CallerActionIds.Append(caller.ActionId).Where(id => id != actionId).Distinct().ToArray();
        }

        public Guid ActionId { get; }
        public string? ActionName { get; }

        /// <summary>The actions whose run led to this one, outermost first (not this one).</summary>
        public IReadOnlyList<Guid> CallerActionIds { get; }

        /// <summary>The engine's context in these variables, or null (none, or something else under its key).</summary>
        public static ActionRunContext? Of(Dictionary<string, object> variables) =>
            variables.TryGetValue(Key, out var value) ? value as ActionRunContext : null;
    }
}
