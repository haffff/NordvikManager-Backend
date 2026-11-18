// selfcheck-player.js for the self-check running in the host worker: the same exports
// (HostSelfCheck's [JSImport]s), run by the real module in the page (it's an RTCPeerConnection).
import { call, notify } from './worker-bridge.js';

const MODULE = 'selfcheck-player';

export const init = () => call(MODULE, '__bind', ['NordvikManager.Wasm.Host', 'NordvikManager.Wasm.Host.SelfCheck.HostSelfCheck']);
export const offer = () => call(MODULE, 'offer');
export const setAnswer = (sdp) => call(MODULE, 'setAnswer', [sdp]);
export const addCandidate = (candidate, sdpMid, index) => notify(MODULE, 'addCandidate', [candidate, sdpMid, index]);
export const waitOpen = () => call(MODULE, 'waitOpen');
export const request = (method, path, bodyJson, queryJson) => call(MODULE, 'request', [method, path, bodyJson, queryJson]);
export const sendCommand = (json) => notify(MODULE, 'sendCommand', [json]);
export const waitForCommand = (command, data) => call(MODULE, 'waitForCommand', [command, data]);
