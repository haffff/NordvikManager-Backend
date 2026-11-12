// The self-check's player: a plain RTCPeerConnection speaking the player client's data channel
// protocol (api-request/api-response, WebSocket-style commands, "chunk" envelopes over 15 000 chars).
const CHUNK = 15000;
let pc, channel, host;
let opened;
const pendingRequests = new Map(); // id -> resolve(response json)
const chunks = new Map();          // chunkId -> { parts, received, total }
const commands = [];               // every command received, parsed
const commandWaiters = [];         // { match, resolve }

export async function init() {
    const exports = await globalThis.getDotnetRuntime(0).getAssemblyExports("NordvikManager.Wasm.Host");
    host = exports.NordvikManager.Wasm.Host.SelfCheck.HostSelfCheck;
}

// When the self-check runs in the host worker: its callbacks go there (page-bridge.js).
export function useCallbacks(target) {
    host = target;
}

export async function offer() {
    pc = new RTCPeerConnection();
    pc.onicecandidate = (e) => {
        if (e.candidate)
            host.OnPlayerCandidate(e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex ?? -1);
    };
    channel = pc.createDataChannel("game");
    opened = new Promise((resolve) => (channel.onopen = () => resolve()));
    channel.onmessage = (ev) => receive(ev.data);
    const o = await pc.createOffer();
    await pc.setLocalDescription(o);
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

export function waitOpen() {
    return opened;
}

function receive(text) {
    let msg = JSON.parse(text);
    if (msg.type === "chunk") {
        let entry = chunks.get(msg.chunkId);
        if (!entry) chunks.set(msg.chunkId, (entry = { parts: new Array(msg.total), received: 0 }));
        if (entry.parts[msg.index] === undefined) entry.received++;
        entry.parts[msg.index] = msg.data;
        if (entry.received < msg.total) return;
        chunks.delete(msg.chunkId);
        msg = JSON.parse(entry.parts.join(""));
        msg.__chunked = true;
    }
    if (msg.type === "api-response") {
        const resolve = pendingRequests.get(msg.id);
        pendingRequests.delete(msg.id);
        if (resolve) resolve(JSON.stringify(msg));
        return;
    }
    commands.push(msg);
    for (const waiter of [...commandWaiters]) {
        if (waiter.match(msg)) {
            commandWaiters.splice(commandWaiters.indexOf(waiter), 1);
            waiter.resolve(JSON.stringify(msg));
        }
    }
}

function send(json) {
    if (json.length <= CHUNK) {
        channel.send(json);
        return;
    }
    const chunkId = crypto.randomUUID();
    const total = Math.ceil(json.length / CHUNK);
    for (let i = 0; i < total; i++)
        channel.send(JSON.stringify({ type: "chunk", chunkId, index: i, total, data: json.slice(i * CHUNK, (i + 1) * CHUNK) }));
}

// An api-request (body and query as JSON text, or empty); resolves with the api-response as JSON.
export function request(method, path, bodyJson, queryJson) {
    const id = crypto.randomUUID();
    const answered = new Promise((resolve) => pendingRequests.set(id, resolve));
    const body = bodyJson ? JSON.parse(bodyJson) : null;
    const query = queryJson ? JSON.parse(queryJson) : {};
    send(JSON.stringify({ type: "api-request", id, method, path, query, body }));
    return answered;
}

export function sendCommand(json) {
    send(json);
}

// Resolves with the first command (as JSON) named `command` whose data is `data`.
export function waitForCommand(command, data) {
    const match = (m) => m.command === command && m.data === data;
    const seen = commands.find(match);
    if (seen) return Promise.resolve(JSON.stringify(seen));
    return new Promise((resolve) => commandWaiters.push({ match, resolve }));
}
