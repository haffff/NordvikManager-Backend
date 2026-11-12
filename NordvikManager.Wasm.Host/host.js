// Starts the browser host for a page: .NET and the games in a worker (host-worker.js), with this
// page doing what a worker can't (RTCPeerConnection: nordvik-webrtc.js, served by page-bridge.js).
// Used by this bundle's own GM page (main.js) and by the frontend's host build, which imports it
// from where it put this bundle (it must be the page's own origin: workers are).
//
//   const { worker, ready } = startHost({ central });
//   const { host } = await ready;          // HostPage's [JSExport]s: Login, Logout, UserName, Call
//   ready rejects with an Error named "locked" when another tab of the site has the games.
import { serve } from './page-bridge.js';
import * as rtc from './nordvik-webrtc.js';

/**
 * @param central   the Central to use (a URL)
 * @param args      the worker's arguments instead of ['gm', central] (the self-check's)
 * @param modules   more page modules for the worker ({ name: module })
 * @param onFailed  the host stopped after it was ready ({ message })
 */
export function startHost({ central, args, modules = {}, onFailed = () => {} } = {}) {
  const worker = new Worker(new URL('./host-worker.js', import.meta.url), { type: 'module' });
  let started = false;
  let bridge;
  const ready = new Promise((resolve, reject) => {
    bridge = serve(worker, { 'nordvik-webrtc': rtc, ...modules }, {
      async ready() {
        started = true;
        // Without it the browser may clear the games when the disk is nearly full.
        const persisted = await navigator.storage?.persist?.().catch(() => false) ?? false;
        console.log(persisted ? 'Persistent storage granted: the browser keeps the games.' : 'Persistent storage not granted: the browser may clear the games when the disk is nearly full.');
        resolve({ host: bridge.host, persisted });
      },
      locked() {
        const locked = new Error('Your games are open in another tab of this site. Use that tab, or close it and reload this one.');
        locked.name = 'locked';
        reject(locked);
      },
      failed({ message }) {
        if (started) onFailed({ message });
        else reject(new Error(message));
      },
    });
  });

  // Let go of the games' files at once when the page goes away for good (a reload, or closing it),
  // so the next page can open them. Not when it's kept for back/forward: its worker lives on.
  addEventListener('pagehide', (e) => { if (!e.persisted) bridge.post({ kind: 'close' }); });

  bridge.post({ kind: 'start', args: args ?? ['gm', central] });
  return { worker, ready };
}
