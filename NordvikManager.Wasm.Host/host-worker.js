// The browser host's worker: .NET runs here, off the page's thread, with its SQLite files on OPFS
// (opfs-fs.js, mounted at /opfs). The page (main.js) starts it with { kind: 'start', args }:
//   ['gm', centralUrl]           the GM host (HostPage): the games in /opfs/data.db
//   ['selfcheck']                the self-check, on its own /opfs/selfcheck.db
//   ['selfcheck', 'crash-write' | 'crash-check']   storage-e2e.cjs, on /opfs/crash.db
// What a worker lacks (RTCPeerConnection) the page does, through worker-bridge.js.
import { dotnet } from './_framework/dotnet.js';
import { mountOpfs } from './opfs-fs.js';
import { on, post } from './worker-bridge.js';

const FILES = {
  gm: ['data.db', 'data.db-journal'],
  selfcheck: ['selfcheck.db', 'selfcheck.db-journal'],
  crash: ['crash.db', 'crash.db-journal'],
};

// Before OPFS, the GM's games were a copy of data.db kept in IndexedDB. Moved once, into an empty
// data.db; the IndexedDB copy is deleted only after the host has opened (and migrated) the games.
const LEGACY_DB = 'nordvik-host', LEGACY_STORE = 'files', LEGACY_KEY = 'data.db';

function legacyStore(mode, action) {
  return new Promise((resolve) => {
    const open = indexedDB.open(LEGACY_DB, 1);
    open.onupgradeneeded = () => open.result.createObjectStore(LEGACY_STORE);
    open.onerror = () => resolve(null);
    open.onsuccess = () => {
      const db = open.result;
      const tx = db.transaction(LEGACY_STORE, mode);
      const request = action(tx.objectStore(LEGACY_STORE));
      tx.oncomplete = () => { db.close(); resolve(request.result ?? null); };
      tx.onerror = tx.onabort = () => { db.close(); resolve(null); };
    };
  });
}

let moved = false;
async function seedFromIndexedDb(name) {
  if (name !== 'data.db') return null;
  const value = await legacyStore('readonly', (store) => store.get(LEGACY_KEY));
  if (!value) return null;
  moved = true;
  console.log(`Moving your games from IndexedDB to OPFS (${value.byteLength} bytes).`);
  return new Uint8Array(value);
}

on('host-ready', async () => {
  if (!moved) return;
  await legacyStore('readwrite', (store) => store.delete(LEGACY_KEY));
  console.log('Your games are on OPFS now; the old IndexedDB copy is deleted.');
});

// The host's log for the page (host.js keeps it, so it outlives this worker): every console line
// here, .NET's included (its Console goes to console.log). The console keeps them too (DevTools).
for (const level of ['log', 'info', 'warn', 'error', 'debug']) {
  const original = console[level].bind(console);
  console[level] = (...args) => {
    original(...args);
    try {
      const text = args.map((a) => (typeof a === 'string' ? a : a instanceof Error ? `${a.name}: ${a.message}
${a.stack ?? ''}` : (() => { try { return JSON.stringify(a); } catch { return String(a); } })())).join(' ');
      post({ kind: 'log', level, text, at: Date.now() });
    } catch { /* the log can't stop the host */ }
  };
}

let mounted = null;
on('close', () => mounted?.close());

on('start', async ({ args }) => {
  try {
    const runtime = await dotnet.withApplicationArguments(...args).create();
    const files = args[0] === 'gm' ? FILES.gm : args[1]?.startsWith('crash') ? FILES.crash : FILES.selfcheck;
    try {
      mounted = await mountOpfs(runtime.Module.FS, '/opfs', files, { seed: args[0] === 'gm' ? seedFromIndexedDb : undefined });
    } catch (err) {
      if (err.name === 'locked') return post({ kind: 'locked', message: err.message });
      throw err;
    }
    await runtime.runMain();
  } catch (err) {
    console.error('The host stopped:', err);
    post({ kind: 'failed', message: String(err?.message ?? err) });
  }
});
