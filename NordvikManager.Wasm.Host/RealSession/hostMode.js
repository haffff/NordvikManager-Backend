// gm.html sets window.__nordvikMode = 'gm' in an inline script before loading
// main.js as a module (inline classic scripts run before deferred module scripts,
// so this always happens before dotnet.run() / Program.cs starts). index.html sets
// nothing, so this defaults to 'harness' — index.html itself stays untouched.
export function getMode() {
  return window.__nordvikMode || 'harness';
}
