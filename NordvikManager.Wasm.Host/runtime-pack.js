// The runtime's files from one download (see the csproj's PackRuntimeFiles): _framework/runtime-pack.json
// names the pack (runtime.<hash>.pack.gz), which is fetched once, kept in Cache Storage under that
// name, and handed to the runtime file by file (dotnet.withResourceLoader). A dozen requests to
// start instead of ~250, so a host that rate-limits bursts (429) lets them through; and after the
// first visit, none for the runtime at all. Anything wrong with the pack: null, and the runtime
// loads its files one by one as it otherwise would.

const CACHE = 'nordvik-host-runtime';

async function gunzipIfNeeded(buffer) {
  const bytes = new Uint8Array(buffer);
  // A server may have sent it with Content-Encoding: gzip, which fetch has already undone.
  if (bytes[0] !== 0x1f || bytes[1] !== 0x8b) return bytes;
  const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

/** Map of path (relative to _framework, '/') -> bytes; or null to load the files one by one. */
export async function loadRuntimePack(base = new URL('./_framework/', import.meta.url)) {
  try {
    const manifest = await (await fetch(new URL('runtime-pack.json', base), { cache: 'no-cache' })).json();
    const url = new URL(manifest.file, base).href;

    let response = null;
    let cache = null;
    try {
      cache = await caches.open(CACHE);
      response = await cache.match(url);
    } catch { /* no Cache Storage: just download it */ }
    if (!response) {
      response = await fetch(url);
      if (!response.ok) throw new Error(`${manifest.file}: ${response.status}`);
      if (cache) {
        await cache.put(url, response.clone()).catch(() => {});
        // Only the current pack is kept.
        for (const old of await cache.keys()) if (old.url !== url) await cache.delete(old);
      }
    }

    const bytes = await gunzipIfNeeded(await response.arrayBuffer());
    if (new TextDecoder().decode(bytes.subarray(0, 4)) !== 'NVPK') throw new Error('not a runtime pack');
    const indexLength = new DataView(bytes.buffer, bytes.byteOffset + 4, 4).getUint32(0, true);
    const index = JSON.parse(new TextDecoder().decode(bytes.subarray(8, 8 + indexLength)));
    const start = 8 + indexLength;
    const files = new Map(index.map((f) => [f.path, bytes.subarray(start + f.offset, start + f.offset + f.length)]));
    console.log(`Runtime: ${files.size} files from ${manifest.file}.`);
    return files;
  } catch (err) {
    console.warn('Runtime pack not used, loading the files one by one:', err?.message ?? err);
    return null;
  }
}

/** A dotnet.withResourceLoader callback serving `files`; the rest the runtime fetches itself. */
export function servePack(files) {
  return (type, name, defaultUri) => {
    const path = new URL(defaultUri, self.location.href).pathname.split('/_framework/').pop();
    const bytes = files.get(path) ?? files.get(name);
    if (!bytes) return null;
    return Promise.resolve(new Response(bytes, {
      headers: { 'Content-Type': path.endsWith('.wasm') ? 'application/wasm' : 'application/octet-stream' },
    }));
  };
}
