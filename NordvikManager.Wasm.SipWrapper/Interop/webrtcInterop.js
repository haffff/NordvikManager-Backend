// General-purpose bridge between browser-native WebRTC and NordvikManager.Wasm.SipWrapper's
// C# types (RTCPeerConnection, RTCDataChannel). Every peer connection / data channel is
// tracked here by a string id handed back to C# — all calls after creation are keyed by
// that id rather than object references, since [JSExport]/[JSImport] marshaling only
// supports simple/serializable types, not opaque JS object handles.
//
// The consuming host must set `globalThis.__nordvikWebRtcExports` to the SipWrapper
// assembly's JS exports (`await dotnet.getAssemblyExports("NordvikManager.Wasm.SipWrapper")`)
// BEFORE any of this module's exported functions are called.

let nextId = 0;
const peerConnections = new Map(); // id -> RTCPeerConnection
const dataChannels = new Map();    // id -> RTCDataChannel

function bridge() {
    const exp = globalThis.__nordvikWebRtcExports;
    if (!exp) throw new Error("webrtcInterop.js: globalThis.__nordvikWebRtcExports not set before use");
    return exp.NordvikManager.Wasm.SipWrapper.Interop.WebRtcBridge;
}

function wireDataChannel(channelId, channel) {
    // Tell C# about the channel FIRST — OnDataChannel constructs the RTCDataChannel
    // wrapper and runs WebRTCSessionService.cs's `pc.ondatachannel += ...` handler
    // synchronously, which subscribes onopen/onclose/onmessage. Only after that is it
    // safe to attach the JS-native listeners below: firing OnDataChannelOpen before
    // OnDataChannel has run would look up an id the C# registry doesn't have yet and
    // silently drop the event.
    dataChannels.set(channelId, channel);
    bridge().OnDataChannel(channel.__pcId, channelId);

    channel.onopen = () => bridge().OnDataChannelOpen(channelId);
    channel.onclose = () => bridge().OnDataChannelClose(channelId);
    channel.onmessage = (ev) => {
        const bytes = typeof ev.data === "string"
            ? new TextEncoder().encode(ev.data)
            : new Uint8Array(ev.data);
        bridge().OnDataChannelMessage(channelId, bytes);
    };
    // Late-subscribe race: the channel may already be open by the time we get here
    // (mirrors the same race WebRTCSessionService.cs already guards against on the C# side).
    if (channel.readyState === "open") bridge().OnDataChannelOpen(channelId);
}

export function createPeerConnection(iceServersJson) {
    const iceServers = JSON.parse(iceServersJson);
    const pc = new RTCPeerConnection(iceServers.length > 0 ? { iceServers } : undefined);
    const id = `pc-${nextId++}`;
    peerConnections.set(id, pc);

    pc.onicecandidate = (e) => {
        const c = e.candidate;
        bridge().OnIceCandidate(
            id,
            c ? c.candidate : null,
            c ? c.sdpMid : null,
            c ? (c.sdpMLineIndex ?? 0) : 0);
    };
    pc.onconnectionstatechange = () => bridge().OnConnectionStateChange(id, pc.connectionState);
    pc.ondatachannel = (ev) => {
        const channelId = `dc-${nextId++}`;
        ev.channel.__pcId = id;
        wireDataChannel(channelId, ev.channel);
    };

    return id;
}

export function closePeerConnection(id) {
    const pc = peerConnections.get(id);
    if (pc) pc.close();
    peerConnections.delete(id);
}

export async function setRemoteDescription(id, type, sdp) {
    const pc = peerConnections.get(id);
    try {
        await pc.setRemoteDescription({ type, sdp });
        return "OK";
    } catch (err) {
        console.error("[nordvik-webrtc] setRemoteDescription failed:", err);
        return "Error";
    }
}

export async function createAnswer(id) {
    const pc = peerConnections.get(id);
    const answer = await pc.createAnswer();
    return answer.sdp;
}

export async function setLocalDescription(id, type, sdp) {
    const pc = peerConnections.get(id);
    await pc.setLocalDescription({ type, sdp });
}

export async function addIceCandidate(id, candidate, sdpMid, sdpMLineIndex) {
    const pc = peerConnections.get(id);
    await pc.addIceCandidate({ candidate, sdpMid: sdpMid || null, sdpMLineIndex });
}

export function dataChannelSend(channelId, text) {
    const channel = dataChannels.get(channelId);
    if (channel) channel.send(text);
}
