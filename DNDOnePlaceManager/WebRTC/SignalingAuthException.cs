using System;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// The Central Server rejected this backend's signaling authentication
    /// (e.g. outdated protocol version). The message comes from the Central Server
    /// and is meant to be shown to the GM as-is.
    /// </summary>
    public class SignalingAuthException : Exception
    {
        public SignalingAuthException(string message) : base(message) { }
    }
}
