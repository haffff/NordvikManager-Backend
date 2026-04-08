using System;
using System.Threading;

namespace DNDOnePlaceManager.Models
{
    public enum ActionRunState
    {
        Running,
        WaitingForInput,
        Completed,
        Faulted,
        Killed
    }

    public class ActionRunEntry
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public Guid RunId { get; init; } = Guid.NewGuid();
        public string ActionName { get; init; }
        public DateTime StartedAt { get; init; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; private set; }
        public ActionRunState State { get; private set; } = ActionRunState.Running;
        public string CurrentStep { get; private set; }
        public string FaultMessage { get; private set; }
        /// <summary>The InputToken this action is currently blocked on, if any.</summary>
        public Guid? WaitingOnToken { get; private set; }

        /// <summary>Cancellation token observed by the action execution loop.</summary>
        public CancellationToken CancellationToken => _cts.Token;

        public void SetStep(string stepType) => CurrentStep = stepType;

        public void SetWaitingForInput(Guid token)
        {
            State = ActionRunState.WaitingForInput;
            WaitingOnToken = token;
        }

        public void ClearWaitingForInput()
        {
            State = ActionRunState.Running;
            WaitingOnToken = null;
        }
        public void SetCompleted()
        {
            State = ActionRunState.Completed;
            FinishedAt = DateTime.UtcNow;
            CurrentStep = null;
            WaitingOnToken = null;
        }

        public void SetFaulted(string message)
        {
            State = ActionRunState.Faulted;
            FinishedAt = DateTime.UtcNow;
            FaultMessage = message;
            CurrentStep = null;
            WaitingOnToken = null;
        }

        /// <summary>
        /// Signals cancellation. The execution loop checks <see cref="CancellationToken"/> at each step boundary.
        /// If the action is currently blocked on input the waiting TCS will also be cancelled via the token.
        /// </summary>
        public void Kill()
        {
            State = ActionRunState.Killed;
            FinishedAt = DateTime.UtcNow;
            CurrentStep = null;
            WaitingOnToken = null;
            _cts.Cancel();
        }
    }
}
