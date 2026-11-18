// The browser half of NordvikManager.Wasm.WebRtc (JsBrowserRtc.cs): the GM host's peer
// connections are the browser's own RTCPeerConnection. Everything is keyed by string id,
// since JS interop can't hand object references to C#. Peer ids come from C#; data channel
// ids are made here. Events go back through JsBrowserRtc's [JSExport] callbacks: in this page
// (init), or, when .NET runs in a worker, through whatever useCallbacks was given (the browser
// host's page bridge, which also wants each channel's bufferedAmount reported: the worker can't
// read it synchronously).

const peers = new Map();    // peerId -> RTCPeerConnection
const channels = new Map(); // channelId -> { channel, peerId }
let nextChannel = 0;
let callbacks = null;
let reportBuffered = false;
// Below this the channel says so (bufferedamountlow), for a worker waiting to send more.
const BUFFERED_LOW = 256 * 1024;

// C# callbacks run synchronously inside browser events; one throwing must not break the page.
function call(name, ...args) {
    try {
        callbacks[name](...args);
    } catch (err) {
        console.error(`[nordvik-webrtc] ${name} failed:`, err);
    }
}

export async function init(assemblyName) {
    const runtime = globalThis.getDotnetRuntime && globalThis.getDotnetRuntime(0);
    if (!runtime)
        throw new Error("nordvik-webrtc: the .NET runtime is not started");
    const exports = await runtime.getAssemblyExports(assemblyName);
    callbacks = exports.NordvikManager.Wasm.WebRtc.JsBrowserRtc;
}

// Callbacks somewhere else (a worker's .NET, through a bridge). With reportBuffered, each
// channel's bufferedAmount goes to callbacks.$buffered(channelId, amount, sendSeq) after every
// send (sendSeq: the send's) and when it falls below BUFFERED_LOW (sendSeq null).
export function useCallbacks(target, options) {
    callbacks = target;
    reportBuffered = !!(options && options.reportBuffered);
}

function buffered(channelId, channel, seq) {
    if (!reportBuffered)
        return;
    try {
        callbacks.$buffered(channelId, channel.bufferedAmount, seq);
    } catch (err) {
        console.error("[nordvik-webrtc] $buffered failed:", err);
    }
}

function peer(peerId) {
    const pc = peers.get(peerId);
    if (!pc)
        throw new Error(`nordvik-webrtc: no peer ${peerId}`);
    return pc;
}

export function createPeer(peerId, iceServersJson) {
    const iceServers = JSON.parse(iceServersJson);
    const pc = new RTCPeerConnection(iceServers.length > 0 ? { iceServers } : undefined);
    peers.set(peerId, pc);

    pc.onconnectionstatechange = () => call("OnStateChanged", peerId, pc.connectionState);
    pc.onicecandidate = (e) => {
        const c = e.candidate;
        call("OnIceCandidate", peerId, c ? c.candidate : null, c ? c.sdpMid : null, c && c.sdpMLineIndex != null ? c.sdpMLineIndex : -1);
    };
    pc.ondatachannel = (e) => {
        const channel = e.channel;
        const channelId = `${peerId}-dc-${nextChannel++}`;
        channel.binaryType = "arraybuffer";
        channels.set(channelId, { channel, peerId });
        // C# first, so it knows the channel before any of its events; whether it is already
        // open goes with it, since its open event may have passed.
        call("OnDataChannel", peerId, channelId, channel.readyState === "open");
        channel.onopen = () => call("OnChannelOpen", channelId);
        channel.onclose = () => {
            channels.delete(channelId);
            call("OnChannelClose", channelId);
        };
        if (reportBuffered) {
            channel.bufferedAmountLowThreshold = BUFFERED_LOW;
            channel.onbufferedamountlow = () => buffered(channelId, channel, null);
        }
        channel.onmessage = (ev) => {
            const text = typeof ev.data === "string" ? ev.data : new TextDecoder().decode(ev.data);
            call("OnChannelMessage", channelId, text);
        };
    };
}

// Null when accepted, otherwise the browser's error.
export async function setRemoteOffer(peerId, sdp) {
    try {
        await peer(peerId).setRemoteDescription({ type: "offer", sdp });
        return null;
    } catch (err) {
        return `${err && err.name ? err.name : "Error"}: ${err && err.message ? err.message : err}`;
    }
}

export async function createAnswer(peerId) {
    const pc = peer(peerId);
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    return pc.localDescription.sdp;
}

export function addIceCandidate(peerId, candidate, sdpMid, sdpMLineIndex) {
    const pc = peers.get(peerId);
    if (!pc)
        return;
    const init = { candidate, sdpMid: sdpMid || null };
    if (sdpMLineIndex >= 0)
        init.sdpMLineIndex = sdpMLineIndex;
    pc.addIceCandidate(init).catch((err) => console.warn(`[nordvik-webrtc] addIceCandidate failed for ${peerId}:`, err));
}

export function closePeer(peerId) {
    const pc = peers.get(peerId);
    peers.delete(peerId);
    for (const [channelId, entry] of channels) {
        if (entry.peerId === peerId)
            channels.delete(channelId);
    }
    if (pc)
        pc.close();
}

// Throws (InvalidStateError) when the channel is not open; C# treats that as not sent.
// seq: the worker's number for this send, echoed with the bufferedAmount after it.
export function send(channelId, message, seq) {
    const entry = channels.get(channelId);
    if (!entry)
        throw new Error(`nordvik-webrtc: no channel ${channelId}`);
    try {
        entry.channel.send(message);
    } finally {
        buffered(channelId, entry.channel, seq === undefined ? null : seq);
    }
}

export function bufferedAmount(channelId) {
    const entry = channels.get(channelId);
    return entry ? entry.channel.bufferedAmount : 0;
}
