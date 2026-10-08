namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Version of the protocol between this backend and the player client
    /// (WebRTC data channel commands and tunneled REST shapes).
    ///
    /// Sent to the Central Server when the GM authenticates; players are switched to the
    /// frozen player build for this number (/client/p{N}/) before they connect.
    ///
    /// Bump it only for changes an older player client cannot handle, and bump the frontend's
    /// src/protocol.json to the same value in the same release (CI checks they match).
    /// </summary>
    public static class ProtocolVersion
    {
        public const int Current = 1;
    }
}
