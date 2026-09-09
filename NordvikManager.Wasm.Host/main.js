import { dotnet } from './_framework/dotnet.js'

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();

// Two separate export sets: the main assembly's (Program.cs, SessionServiceHarness —
// what testSignaling.js calls into) and NordvikManager.Wasm.SipWrapper's own
// [JSExport]s (what webrtcInterop.js calls into). getAssemblyExports must be called
// once per assembly that contains [JSExport] methods you intend to call from JS —
// the POC this evolved from only ever fetched the main assembly's exports, which is
// not enough once WebRTC logic lives in a separate referenced project.
globalThis.__dotnetExports = await getAssemblyExports(config.mainAssemblyName);
globalThis.__nordvikWebRtcExports = await getAssemblyExports('NordvikManager.Wasm.SipWrapper');

await dotnet.run();
