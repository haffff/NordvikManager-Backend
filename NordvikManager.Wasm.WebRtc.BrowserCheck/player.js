// A stand-in for a player's browser: a plain RTCPeerConnection that offers, like the player client.
let pc, channel, harness;

function cb() {
    return harness;
}

export async function init() {
    const exports = await globalThis.getDotnetRuntime(0).getAssemblyExports("WasmRtcHarness");
    harness = exports.Harness;
}

export async function offer() {
    pc = new RTCPeerConnection();
    pc.onicecandidate = (e) => {
        if (e.candidate)
            cb().OnPlayerCandidate(e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex ?? -1);
    };
    pc.onconnectionstatechange = () => console.log("[player] state", pc.connectionState);
    channel = pc.createDataChannel("game");
    channel.onmessage = (ev) => {
        console.log("[player] got", ev.data);
        channel.send("ack:" + ev.data + ":zażółć 🎲");
    };
    channel.onclose = () => cb().OnPlayerChannelClosed();
    const o = await pc.createOffer();
    const gathered = new Promise((resolve) => {
        pc.onicegatheringstatechange = () => { if (pc.iceGatheringState === "complete") resolve(); };
    });
    await pc.setLocalDescription(o);
    // The offer as created holds no candidates: every candidate goes separately, all of them
    // before the GM sets the offer, so connecting needs its early-candidate queue.
    await gathered;
    return o.sdp;
}

export async function setAnswer(sdp) {
    await pc.setRemoteDescription({ type: "answer", sdp });
}

export function addCandidate(candidate, sdpMid, index) {
    const init = { candidate, sdpMid };
    if (index >= 0) init.sdpMLineIndex = index;
    pc.addIceCandidate(init).catch((e) => console.log("[player] addIceCandidate failed", e));
}
