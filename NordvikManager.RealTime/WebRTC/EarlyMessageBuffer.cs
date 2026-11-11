using System;
using System.Collections.Generic;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Holds what a player's data channel receives before the backend knows who the player is.
    /// The client sends as soon as its channel opens (its queued requests: whoami, the battle
    /// map...), while the backend is still looking the player up or creating them. Those
    /// messages are kept here and handled, in order, once the player is known.
    /// </summary>
    public class EarlyMessageBuffer
    {
        public enum Route
        {
            /// <summary>The player is known: handle the message now.</summary>
            Handle,
            /// <summary>Kept until <see cref="Open"/>.</summary>
            Held,
            /// <summary>The channel failed, or too much arrived before it opened.</summary>
            Dropped,
        }

        // A client sends a handful of requests on connect; far more than this before the player
        // is known isn't a client waiting for its answers.
        public const int DefaultCapacity = 500;

        private enum State { Pending, Open, Failed }

        private readonly object _lock = new();
        private readonly int _capacity;
        private List<byte[]> _held = new();
        private State _state = State.Pending;

        public EarlyMessageBuffer(int capacity = DefaultCapacity)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// Where a received message goes. While <see cref="Open"/> replays the held messages,
        /// this waits for it, so a new message never overtakes an older one.
        /// </summary>
        public Route Receive(byte[] data)
        {
            lock (_lock)
            {
                switch (_state)
                {
                    case State.Open:
                        return Route.Handle;
                    case State.Failed:
                        return Route.Dropped;
                    default:
                        if (_held.Count >= _capacity)
                            return Route.Dropped;
                        _held.Add(data);
                        return Route.Held;
                }
            }
        }

        /// <summary>The player is known: hands each held message to handle, in order.</summary>
        public void Open(Action<byte[]> handle)
        {
            lock (_lock)
            {
                if (_state != State.Pending)
                    return;
                var held = _held;
                _held = new List<byte[]>();
                foreach (var data in held)
                    handle(data);
                _state = State.Open;
            }
        }

        /// <summary>The player couldn't be resolved: returns the held messages, to be answered with an error.</summary>
        public IReadOnlyList<byte[]> Fail()
        {
            lock (_lock)
            {
                if (_state != State.Pending)
                    return Array.Empty<byte[]>();
                var held = _held;
                _held = new List<byte[]>();
                _state = State.Failed;
                return held;
            }
        }
    }
}
