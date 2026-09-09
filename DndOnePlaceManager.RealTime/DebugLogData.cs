using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Typed payload sent to the client during debug-mode action execution.
    /// Replaces the anonymous objects previously passed to DebugLog().
    /// </summary>
    public class DebugLogData
    {
        public ActionDto Action { get; set; }
        public object Step { get; set; }
        public object Variables { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }

        // ── Factory helpers ─────────────────────────────────────────────────

        public static DebugLogData Starting(ActionDto action, JArray steps, object variables)
            => new DebugLogData { Action = action, Step = steps, Variables = variables, Message = WebSockets.Core.WebSocketCommandNames.DebugMsgStarting };

        public static DebugLogData ExecutingStep(ActionDto action, JToken step, object variables)
            => new DebugLogData { Action = action, Step = step, Variables = variables, Message = WebSockets.Core.WebSocketCommandNames.DebugMsgExecutingStep + step["Type"] };

        public static DebugLogData Finishing(ActionDto action, JArray steps, object variables)
            => new DebugLogData { Action = action, Step = steps, Variables = variables, Message = WebSockets.Core.WebSocketCommandNames.DebugMsgFinishing };

        public static DebugLogData StepNotFound(JToken step)
            => new DebugLogData { Error = WebSockets.Core.WebSocketCommandNames.DebugErrStepNotFound, Step = step };

        public static DebugLogData Fault(string errorMessage)
            => new DebugLogData { Error = errorMessage };
    }
}
