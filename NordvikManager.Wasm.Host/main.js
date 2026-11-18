// The browser host's own page: a small GM page (gm-page.js) over the host (host.js: .NET and the
// games in a worker, the RTCPeerConnections here). The frontend's host build is the full GM
// interface over the same host.js.
//   ?central=<url>   the Central to use (default: this page's origin)
//   ?selfcheck       the self-check instead of the GM page (results in the console);
//                    ?selfcheck=crash-write / crash-check are storage-e2e.cjs's
import { startHost } from './host.js';

const params = new URLSearchParams(location.search);
const central = (params.get('central') || location.origin).replace(/\/+$/, '');
const selfcheck = params.has('selfcheck');

function status(text, kind = 'info') {
  const line = document.querySelector('#status');
  line.textContent = text;
  line.dataset.kind = kind;
  console.log(`[page] ${text}`);
}

const { worker, ready } = selfcheck
  ? startHost({
    args: ['selfcheck', ...(params.get('selfcheck') ? [params.get('selfcheck')] : [])],
    modules: { 'selfcheck-player': await import('./selfcheck-player.js') },
  })
  : startHost({ central, onFailed: ({ message }) => status(`The host stopped: ${message}`, 'error') });
globalThis.nordvikHostWorker = worker; // storage-e2e.cjs kills it mid-commit

// The self-check never says ready (it prints its results instead).
if (selfcheck) {
  ready.catch((err) => status(`The host stopped: ${err.message}`, 'error'));
} else {
  ready.then(async ({ host }) => {
    globalThis.nordvikHost = host; // gm-e2e.cjs calls the API as the GM (a big upload)
    (await import('./gm-page.js')).start(central, host);
  }, (err) => status(err.name === 'locked' ? err.message : `The host stopped: ${err.message}`, 'error'));
}
