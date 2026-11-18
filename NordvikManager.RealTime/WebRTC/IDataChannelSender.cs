namespace NordvikManager.WebRTC
{
    /// <summary>
    /// The part of a data channel <see cref="WebRTCPlayerConnection"/> sends through —
    /// an interface so the pacing can be tested without a real WebRTC connection.
    /// </summary>
    public interface IDataChannelSender
    {
        bool IsOpen { get; }

        /// <summary>Bytes handed to the channel but not yet sent.</summary>
        ulong BufferedAmount { get; }

        void Send(string message);
    }
}
