// The worker half of the bridge between the host worker (.NET, SQLite on OPFS) and its page
// (the UI, and the browser APIs a worker doesn't have: RTCPeerConnection). page-bridge.js is the
// other half. Messages, by kind:
//   worker → page  call {id, module, fn, args}   run a page module's function, answer with result
//                  notify {module, fn, args}     the same, no answer
//                  ready {central} / locked {message} / failed {message}
//                  result {id, value | error}    the answer to a host call
//   page → worker  start {args}                  start .NET with these arguments
//                  result {id, value | error}    the answer to a call
//                  callback {assembly, type, fn, args}  a page module calling .NET ([JSExport])
//                  host {id, fn, args}           the page calling HostPage's [JSExport]s
//                  close                         the page is going away
//                  anything else                 to the listeners registered with on()
// Order is kept both ways: a call is made in the order it was posted.

const pending = new Map(); // call id -> { resolve, reject }
let nextId = 0;
const listeners = new Map(); // kind -> [fn]
const exportsByAssembly = new Map();

export function post(message) {
  self.postMessage(message);
}

/** Runs `fn` of the page module `module`; resolves with what it returns (or rejects with its error). */
export function call(module, fn, args = []) {
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    post({ kind: 'call', id, module, fn, args });
  });
}

/** Runs `fn` of the page module `module`, without waiting for it. */
export function notify(module, fn, args = []) {
  post({ kind: 'notify', module, fn, args });
}

export function on(kind, fn) {
  if (!listeners.has(kind)) listeners.set(kind, []);
  listeners.get(kind).push(fn);
}

async function assemblyExports(assembly) {
  if (!exportsByAssembly.has(assembly))
    exportsByAssembly.set(assembly, globalThis.getDotnetRuntime(0).getAssemblyExports(assembly));
  return exportsByAssembly.get(assembly);
}

async function exported(assembly, type, fn) {
  let target = await assemblyExports(assembly);
  for (const part of type.split('.')) target = target[part];
  if (typeof target?.[fn] !== 'function') throw new Error(`no [JSExport] ${type}.${fn} in ${assembly}`);
  return target[fn].bind(target);
}

self.onmessage = async ({ data }) => {
  switch (data.kind) {
    case 'result': {
      const waiting = pending.get(data.id);
      pending.delete(data.id);
      if (!waiting) return;
      if ('error' in data) waiting.reject(new Error(data.error));
      else waiting.resolve(data.value);
      return;
    }
    case 'callback':
      try {
        (await exported(data.assembly, data.type, data.fn))(...data.args);
      } catch (err) {
        console.error(`[worker-bridge] ${data.type}.${data.fn} failed:`, err);
      }
      return;
    case 'host':
      try {
        const value = await (await exported('NordvikManager.Wasm.Host', 'NordvikManager.Wasm.Host.HostPage', data.fn))(...data.args);
        post({ kind: 'result', id: data.id, value });
      } catch (err) {
        post({ kind: 'result', id: data.id, error: String(err?.message ?? err) });
      }
      return;
    default:
      for (const fn of listeners.get(data.kind) ?? []) fn(data);
  }
};

// ---- [JSImport]s of HostPage (module "host-bridge") ----

/** The host is built and its games open: the page can show its UI. */
export function ready(central) {
  post({ kind: 'ready', central });
  for (const fn of listeners.get('host-ready') ?? []) fn();
}
