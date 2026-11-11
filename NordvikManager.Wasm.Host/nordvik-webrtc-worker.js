// nordvik-webrtc.js for .NET in the host worker: the same exports (JsBrowserRtc's [JSImport]s),
// each run by the real module in the page, where RTCPeerConnection is (worker-bridge.js). Its
// callbacks come back to JsBrowserRtc's [JSExport]s in this worker.
//
// bufferedAmount must answer synchronously (the send pump polls it), so it's mirrored: the
// amount the page last reported, plus what was sent since that the page hasn't reported on.
import { call, notify, on } from './worker-bridge.js';

const MODULE = 'nordvik-webrtc';
const reported = new Map(); // channelId -> bufferedAmount the page last reported
const unreported = new Map(); // channelId -> Map(seq -> length) sent, not yet reported on
let nextSeq = 0;

on('buffered', ({ args: [channelId, amount, seq] }) => {
  reported.set(channelId, amount);
  const sent = unreported.get(channelId);
  if (sent && seq != null) {
    for (const s of [...sent.keys()]) if (s <= seq) sent.delete(s);
  }
});

export function init(assemblyName) {
  return call(MODULE, '__bind', [assemblyName, 'NordvikManager.Wasm.WebRtc.JsBrowserRtc', { reportBuffered: true }]);
}

export function createPeer(peerId, iceServersJson) {
  notify(MODULE, 'createPeer', [peerId, iceServersJson]);
}

export function setRemoteOffer(peerId, sdp) {
  return call(MODULE, 'setRemoteOffer', [peerId, sdp]);
}

export function createAnswer(peerId) {
  return call(MODULE, 'createAnswer', [peerId]);
}

export function addIceCandidate(peerId, candidate, sdpMid, sdpMLineIndex) {
  notify(MODULE, 'addIceCandidate', [peerId, candidate, sdpMid, sdpMLineIndex]);
}

export function closePeer(peerId) {
  notify(MODULE, 'closePeer', [peerId]);
  for (const channelId of [...reported.keys(), ...unreported.keys()]) {
    if (channelId.startsWith(`${peerId}-dc-`)) {
      reported.delete(channelId);
      unreported.delete(channelId);
    }
  }
}

// A channel that isn't open throws in the page, after this returned: the pump finds out from
// the channel's close event, which follows.
export function send(channelId, message) {
  const seq = nextSeq++;
  if (!unreported.has(channelId)) unreported.set(channelId, new Map());
  unreported.get(channelId).set(seq, message.length);
  notify(MODULE, 'send', [channelId, message, seq]);
}

export function bufferedAmount(channelId) {
  let amount = reported.get(channelId) ?? 0;
  for (const length of unreported.get(channelId)?.values() ?? []) amount += length;
  return amount;
}
