// The page half of the bridge to the host worker (see worker-bridge.js for the messages): runs
// the page modules' functions the worker calls, hands their callbacks back to .NET in the worker,
// and lets the page call HostPage. (The worker's console is its own: DevTools shows it.)

/**
 * Serves `modules` ({ name: module }) to `worker`. `on` handles the worker's own messages
 * (ready, locked, failed, ...), by kind.
 */
export function serve(worker, modules, on = {}) {
  const post = (message) => worker.postMessage(message);
  const pendingHost = new Map();

  // fn "$name" is for the worker's JS (a message of kind "name"), not for .NET.
  const callbackTo = (assembly, type) => new Proxy({}, {
    get: (_, fn) => (...args) => (String(fn).startsWith('$')
      ? post({ kind: String(fn).slice(1), args })
      : post({ kind: 'callback', assembly, type, fn, args })),
  });

  // A module's callbacks go to a .NET type in the worker: its useCallbacks(target) gets a stand-in.
  const run = (module, fn, args) => {
    const target = modules[module];
    if (!target) throw new Error(`page-bridge: no module ${module}`);
    if (fn === '__bind') return target.useCallbacks(callbackTo(args[0], args[1]), args[2]);
    if (typeof target[fn] !== 'function') throw new Error(`page-bridge: no ${module}.${fn}`);
    return target[fn](...args);
  };

  worker.onmessage = ({ data }) => {
    switch (data.kind) {
      case 'call':
        Promise.resolve()
          .then(() => run(data.module, data.fn, data.args))
          .then((value) => post({ kind: 'result', id: data.id, value }),
            (err) => post({ kind: 'result', id: data.id, error: `${err?.name ?? 'Error'}: ${err?.message ?? err}` }));
        return;
      case 'notify':
        try {
          run(data.module, data.fn, data.args);
        } catch (err) {
          console.warn(`[page-bridge] ${data.module}.${data.fn} failed:`, err);
        }
        return;
      case 'result': {
        const waiting = pendingHost.get(data.id);
        pendingHost.delete(data.id);
        if (!waiting) return;
        if ('error' in data) waiting.reject(new Error(data.error));
        else waiting.resolve(data.value);
        return;
      }
      default:
        on[data.kind]?.(data);
    }
  };
  worker.onerror = (e) => on.failed?.({ message: e.message ?? String(e) });

  let nextId = 0;
  return {
    /** HostPage's [JSExport]s, as async functions: host.Login(user, password), host.Call(...). */
    host: new Proxy({}, {
      get: (_, fn) => (...args) => new Promise((resolve, reject) => {
        const id = nextId++;
        pendingHost.set(id, { resolve, reject });
        post({ kind: 'host', id, fn, args });
      }),
    }),
    post,
  };
}
