using SIPSorcery.Net;

namespace DNDOnePlaceManager.WebRTC
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

    internal sealed class SipsorceryDataChannelSender : IDataChannelSender
    {
        private readonly RTCDataChannel _dataChannel;

        public SipsorceryDataChannelSender(RTCDataChannel dataChannel) => _dataChannel = dataChannel;

        public bool IsOpen => _dataChannel.readyState == RTCDataChannelState.open;

        public ulong BufferedAmount => _dataChannel.bufferedAmount;

        public void Send(string message) => _dataChannel.send(message);
    }
}
