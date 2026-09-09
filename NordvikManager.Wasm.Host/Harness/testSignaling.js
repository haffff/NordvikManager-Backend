// Test-only stand-in for the real Central Server signaling relay (ISignalingService).
// peerA is a plain JS RTCPeerConnection representing a remote player's browser tab —
// driven independently of NordvikManager.Wasm.SipWrapper, exactly as a real player
// would be. SessionServiceHarness.cs (peerB, driven from wasm via the wrapper)
// exchanges SDP/ICE with it through the functions below instead of a real network hop.
//
// Chunk reassembly here mirrors the real frontend's WebRTCWebHelper.js — in production
// that logic lives in the player's browser, not the backend, so it belongs here (the
// "player" side of this harness) rather than in the C# harness.

let peerA, channelA;
let dotnetHarnessExports;
const chunkBuffers = new Map(); // chunkId -> (string|null)[]

function harness() {
    if (!dotnetHarnessExports) {
        dotnetHarnessExports = globalThis.__dotnetExports.NordvikManager.Wasm.Host.Harness.SessionServiceHarness;
    }
    return dotnetHarnessExports;
}

function handleIncomingFromPeerB(text) {
    let obj = null;
    try { obj = JSON.parse(text); } catch { /* not JSON, treat as a plain message */ }

    if (obj && obj.type === "chunk") {
        let parts = chunkBuffers.get(obj.chunkId);
        if (!parts) {
            parts = new Array(obj.total).fill(null);
            chunkBuffers.set(obj.chunkId, parts);
        }
        parts[obj.index] = obj.data;
        if (parts.every((p) => p !== null)) {
            chunkBuffers.delete(obj.chunkId);
            harness().OnMessageFromPeerB(parts.join(""));
        }
        return;
    }

    harness().OnMessageFromPeerB(text);
}

export async function startPeerA() {
    peerA = new RTCPeerConnection();
    channelA = peerA.createDataChannel("nordvik");
    channelA.onmessage = (ev) => handleIncomingFromPeerB(ev.data);

    peerA.onicecandidate = (e) => {
        if (e.candidate) {
            harness().OnCandidateFromPeerA(e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex ?? 0);
        }
    };

    const offer = await peerA.createOffer();
    await peerA.setLocalDescription(offer);
    return offer.sdp;
}

export async function submitAnswer(sdp) {
    await peerA.setRemoteDescription({ type: "answer", sdp });
}

export async function submitCandidateFromPeerB(candidate, sdpMid, sdpMLineIndex) {
    await peerA.addIceCandidate({ candidate, sdpMid: sdpMid || null, sdpMLineIndex });
}

export function reportDone(ok, message) {
    fetch(`/__done?ok=${ok}&message=${encodeURIComponent(message)}`)
        .catch(() => {})
        .finally(() => window.close());
}

// ---- Real WebRTCSessionService flow (see WebRTCSessionServiceHarness.cs) ----
//
// Unlike startPeerA() above, the real WebRTCSessionService only creates its
// RTCPeerConnection once it's told (via ISignalingService.PeerJoined) that a peer
// joined — which the harness raises directly in C#, not from here. So peerA's
// creation and its offer are one step here (createPeerAOffer), called only *after*
// the harness has already raised that peer-joined notification. Reuses the same
// peerA/channelA module state and submitAnswer/submitCandidateFromPeerB as above —
// safe because the two flows never run concurrently (WebRTCSessionServiceHarness
// runs to completion before SessionServiceHarness starts, see Program.cs).

let dotnetSignalingExports;
function signalingBridge() {
    if (!dotnetSignalingExports) {
        dotnetSignalingExports = globalThis.__dotnetExports.NordvikManager.Wasm.Host.Harness.TestSignalingService;
    }
    return dotnetSignalingExports;
}

const chunkBuffersV2 = new Map();
function handleIncomingFromPeerBv2(text) {
    let obj = null;
    try { obj = JSON.parse(text); } catch { /* not JSON, treat as a plain message */ }

    if (obj && obj.type === "chunk") {
        let parts = chunkBuffersV2.get(obj.chunkId);
        if (!parts) {
            parts = new Array(obj.total).fill(null);
            chunkBuffersV2.set(obj.chunkId, parts);
        }
        parts[obj.index] = obj.data;
        if (parts.every((p) => p !== null)) {
            chunkBuffersV2.delete(obj.chunkId);
            signalingBridge().OnMessageFromPeerB(parts.join(""));
        }
        return;
    }

    signalingBridge().OnMessageFromPeerB(text);
}

// Sends an arbitrary text message from peerA over the already-open data channel —
// used by WebRTCSessionServiceHarness to probe the REST-tunnel dispatcher (api-request
// messages) after the initial chat-message exchange has already proven the channel works.
export async function sendFromPeerA(text) {
    channelA.send(text);
}

export async function createPeerAOffer() {
    peerA = new RTCPeerConnection();
    channelA = peerA.createDataChannel("nordvik");
    channelA.onopen = () => {
        channelA.send(JSON.stringify({ command: "chat_push", data: "hello from real WebRTCSessionService" }));
    };
    channelA.onmessage = (ev) => handleIncomingFromPeerBv2(ev.data);

    peerA.onicecandidate = (e) => {
        if (e.candidate) {
            signalingBridge().OnCandidateFromPeerA(e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex ?? 0);
        }
    };

    const offer = await peerA.createOffer();
    await peerA.setLocalDescription(offer);
    return offer.sdp;
}
