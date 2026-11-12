// The browser host's page. The host itself (.NET, the games on OPFS) runs in a worker
// (host-worker.js); this page is its UI, and does what a worker can't: RTCPeerConnection
// (nordvik-webrtc.js), served to the worker by page-bridge.js.
//   ?central=<url>   the Central to use (default: this page's origin)
//   ?selfcheck       the self-check instead of the GM page (results in the console);
//                    ?selfcheck=crash-write / crash-check are storage-e2e.cjs's
import { serve } from './page-bridge.js';
import * as rtc from './nordvik-webrtc.js';

const params = new URLSearchParams(location.search);
const central = (params.get('central') || location.origin).replace(/\/+$/, '');
const selfcheck = params.has('selfcheck');

function status(text, kind = 'info') {
  const line = document.querySelector('#status');
  line.textContent = text;
  line.dataset.kind = kind;
  console.log(`[page] ${text}`);
}

const modules = { 'nordvik-webrtc': rtc };
if (selfcheck) modules['selfcheck-player'] = await import('./selfcheck-player.js');

const worker = new Worker(new URL('./host-worker.js', import.meta.url), { type: 'module' });
globalThis.nordvikHostWorker = worker; // storage-e2e.cjs kills it mid-commit
const bridge = serve(worker, modules, {
  async ready() {
    if (selfcheck) return;
    const page = await import('./gm-page.js');
    page.start(central, bridge.host);
    globalThis.nordvikHost = bridge.host; // gm-e2e.cjs calls the API as the GM (a big upload)
    // Without it the browser may clear the games when the disk is nearly full.
    const persisted = await navigator.storage?.persist?.().catch(() => false);
    console.log(persisted ? 'Persistent storage granted: the browser keeps the games.' : 'Persistent storage not granted: the browser may clear the games when the disk is nearly full.');
  },
  locked() {
    status('Your games are open in another tab of this site. Use that tab, or close it and reload this one.', 'error');
  },
  failed({ message }) {
    status(`The host stopped: ${message}`, 'error');
  },
});

// Let go of the games' files at once when the page goes away for good (a reload, or closing it),
// so the next page can open them. Not when it's kept for back/forward: its worker lives on.
addEventListener('pagehide', (e) => { if (!e.persisted) bridge.post({ kind: 'close' }); });

bridge.post({ kind: 'start', args: selfcheck ? ['selfcheck', ...(params.get('selfcheck') ? [params.get('selfcheck')] : [])] : ['gm', central] });
