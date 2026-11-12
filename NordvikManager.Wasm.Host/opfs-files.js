// The browser host's file storage on OPFS, for the host worker: JsBrowserFiles (C#) imports it, and
// BrowserFileStorage keeps the GM's big uploads and the files cards save as files under storage/.
// Paths are relative to OPFS's root, with '/'.
//
// Reads and writes go through sync access handles (only in dedicated workers). Opening one is
// async and locks the file, so a file's handle is opened once and kept for later reads; asking
// again while it opens waits for the same one (two ranges of one audio file at once). Moving or
// deleting a file closes its handle first.

const handles = new Map(); // path -> Promise<FileSystemSyncAccessHandle>
const ready = new Map(); // path -> FileSystemSyncAccessHandle, once open

const split = (path) => {
  const parts = path.split('/').filter(Boolean);
  return { folders: parts.slice(0, -1), name: parts[parts.length - 1] };
};

async function folder(names, create) {
  let dir = await navigator.storage.getDirectory();
  for (const name of names) dir = await dir.getDirectoryHandle(name, { create });
  return dir;
}

async function fileHandle(path, create) {
  const { folders, name } = split(path);
  return (await folder(folders, create)).getFileHandle(name, { create });
}

function handle(path) {
  const h = ready.get(path);
  if (!h) throw new Error(`${path} isn't open`);
  return h;
}

function quota(err) {
  if (err?.name === 'QuotaExceededError') {
    const full = new Error('quota: the browser has no room left');
    full.name = 'QuotaExceededError';
    return full;
  }
  return err;
}

function release(path) {
  const h = ready.get(path);
  ready.delete(path);
  handles.delete(path);
  if (h) try { h.flush(); h.close(); } catch { /* already closed */ }
}

/** Every file under `path`, any depth: JSON [{ path, size, modified }]. */
export async function list(path) {
  const out = [];
  let root;
  try { root = await folder(split(path + '/x').folders, false); } catch { return '[]'; }
  const walk = async (dir, prefix) => {
    for await (const [name, entry] of dir.entries()) {
      const full = `${prefix}/${name}`;
      if (entry.kind === 'directory') await walk(entry, full);
      else if (ready.has(full)) out.push({ path: full, size: ready.get(full).getSize(), modified: (await entry.getFile().catch(() => null))?.lastModified ?? 0 });
      else {
        const file = await entry.getFile();
        out.push({ path: full, size: file.size, modified: file.lastModified });
      }
    }
  };
  await walk(root, path.replace(/\/+$/, ''));
  return JSON.stringify(out);
}

export async function open(path, create) {
  if (!handles.has(path)) {
    const opening = fileHandle(path, create).then((f) => f.createSyncAccessHandle());
    handles.set(path, opening);
    opening.then((h) => ready.set(path, h), () => handles.delete(path));
  }
  const h = await handles.get(path);
  ready.set(path, h);
}

export function readAt(path, view, at) {
  const buffer = new Uint8Array(view.length);
  const n = handle(path).read(buffer, { at });
  view.set(n === buffer.length ? buffer : buffer.subarray(0, n));
  return n;
}

export function writeAt(path, view, at) {
  try {
    handle(path).write(view.slice(), { at });
  } catch (err) {
    throw quota(err);
  }
}

export function truncate(path, size) {
  try { handle(path).truncate(size); } catch (err) { throw quota(err); }
}

export function flush(path) { handle(path).flush(); }

export function close(path) { release(path); }

/** Puts `from` at `to` (in the same folder or another), replacing what is there. */
export async function move(from, to) {
  release(from);
  release(to);
  const source = await fileHandle(from, false);
  const { folders, name } = split(to);
  const target = await folder(folders, true);
  try {
    await source.move(target, name);
  } catch (err) {
    // A browser whose move() won't replace a file: take the old one away first.
    await target.removeEntry(name).catch(() => {});
    await source.move(target, name);
  }
}

export async function remove(path) {
  release(path);
  const { folders, name } = split(path);
  try {
    await (await folder(folders, false)).removeEntry(name);
  } catch (err) {
    if (err?.name !== 'NotFoundError') throw err;
  }
}

/** When the file last changed, as list() will say after a reload. */
export async function modified(path) {
  release(path);
  return (await (await fileHandle(path, false)).getFile()).lastModified;
}
