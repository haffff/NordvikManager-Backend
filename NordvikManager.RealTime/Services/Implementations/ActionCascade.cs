using System.Threading;

namespace NordvikManager.Services.Implementations
{
    /// <summary>
    /// Limits one chain of actions: an action, the actions it calls and the hooks it sets off.
    /// Without this, an action calling itself or firing the hook it listens to runs until the
    /// server falls over, and every game on the server with it. The depth limit stops a loop;
    /// the run limit stops a loop that widens, which reaches no great depth before it has
    /// run millions of times.
    /// </summary>
    public sealed class ActionCascade
    {
        // Addons nest two or three actions deep; a hook chain adds one per action in it.
        public const int DefaultMaxDepth = 32;

        // Generous: a loop over every token on a map, each setting off a few hooks, fits.
        public const int DefaultMaxRuns = 10_000;

        private sealed class Limits
        {
            public int MaxDepth;
            public int MaxRuns;
            public int Runs;
            public int Refused;
        }

        private readonly Limits _limits;
        private readonly int _depth;

        private ActionCascade(Limits limits, int depth)
        {
            _limits = limits;
            _depth = depth;
        }

        /// <summary>A new chain, before its first action.</summary>
        public static ActionCascade Start(int maxDepth = DefaultMaxDepth, int maxRuns = DefaultMaxRuns) =>
            new(new Limits { MaxDepth = maxDepth, MaxRuns = maxRuns }, 0);

        /// <summary>Takes one more action under this one, or null if the chain must stop.</summary>
        public ActionCascade TryEnter()
        {
            if (_depth >= _limits.MaxDepth || Interlocked.Increment(ref _limits.Runs) > _limits.MaxRuns)
                return null;
            return new ActionCascade(_limits, _depth + 1);
        }

        /// <summary>True the first time in this chain: a loop is reported once, not per attempt.</summary>
        public bool FirstRefusal() => Interlocked.Exchange(ref _limits.Refused, 1) == 0;
    }
}
