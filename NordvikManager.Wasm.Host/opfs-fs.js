// An emscripten filesystem over OPFS (the origin private file system), for the host worker:
// SQLite's files live on OPFS, read and written in place, instead of in the runtime's memory.
//
// OPFS's synchronous access handles (only in dedicated workers) are what make this possible:
// SQLite's reads and writes are synchronous. Opening a handle is not, so the files are opened up
// front, before .NET opens them (as SQLite's own opfs-sahpool VFS does); only those names can
// exist in this filesystem. Holding a handle locks the file, so another tab of this site can't
// open the same games (mountOpfs throws "locked").
//
// fsync and close flush the handle, which is what makes a commit durable. Deleting a file
// empties it instead (the handle stays open), and an empty file counts as missing: SQLite
// deletes its rollback journal after each commit, and finds a hot one after a crash.

const ENOENT = 44;
const EPERM = 63;
const EINVAL = 28;

// Read by the self-check (HostSelfCheck): how many times a file was flushed, and how many of
// those were SQLite's fsync (a commit).
globalThis.nordvikOpfs = { flushes: 0, fsyncs: 0 };

function flush(entry) {
  entry.handle.flush();
  globalThis.nordvikOpfs.flushes++;
}

// A reload's new worker can start before the old one has let go of the handles: retry for a
// while before deciding another tab has them.
async function openHandle(file, retryMs) {
  const until = Date.now() + retryMs;
  for (let wait = 50; ; wait = Math.min(wait * 2, 500)) {
    try {
      return await file.createSyncAccessHandle();
    } catch (err) {
      if (err?.name !== 'NoModificationAllowedError' || Date.now() >= until) throw err;
      await new Promise((r) => setTimeout(r, wait));
    }
  }
}

/**
 * Mounts `names` (files in the OPFS root) at `mountPoint` in the runtime's filesystem.
 * `seed(name)` may give the bytes of a file that is still empty (e.g. moved from IndexedDB).
 * Throws an Error named "locked" when another tab holds them.
 */
export async function mountOpfs(FS, mountPoint, names, { seed = async () => null, retryMs = 3000 } = {}) {
  const root = await navigator.storage.getDirectory();
  const entries = new Map();
  try {
    for (const name of names) {
      const file = await root.getFileHandle(name, { create: true });
      const handle = await openHandle(file, retryMs);
      entries.set(name, { handle, exists: false });
    }
  } catch (err) {
    entries.forEach((e) => e.handle.close());
    if (err?.name === 'NoModificationAllowedError') {
      const locked = new Error('The games are open in another tab of this site.');
      locked.name = 'locked';
      throw locked;
    }
    throw err;
  }
  for (const [name, entry] of entries) {
    if (entry.handle.getSize() === 0) {
      const bytes = await seed(name);
      if (bytes?.length) {
        entry.handle.write(bytes, { at: 0 });
        flush(entry);
      }
    }
    entry.exists = entry.handle.getSize() > 0;
  }

  const now = () => new Date();
  const attr = (node, size, blocks) => ({
    dev: 1, ino: node.id, mode: node.mode, nlink: 1, uid: 0, gid: 0, rdev: 0, size,
    atime: now(), mtime: now(), ctime: now(), blksize: 4096, blocks,
  });

  const fileNodeOps = {
    getattr(node) {
      const size = node.entry.handle.getSize();
      return attr(node, size, Math.ceil(size / 4096));
    },
    setattr(node, changes) {
      if (changes.size !== undefined) node.entry.handle.truncate(changes.size);
    },
  };
  const fileStreamOps = {
    open() {},
    close(stream) { flush(stream.node.entry); },
    fsync(stream) {
      globalThis.nordvikOpfs.fsyncs++;
      flush(stream.node.entry);
    },
    read(stream, buffer, offset, length, position) {
      return stream.node.entry.handle.read(buffer.subarray(offset, offset + length), { at: position });
    },
    write(stream, buffer, offset, length, position) {
      return stream.node.entry.handle.write(buffer.subarray(offset, offset + length), { at: position });
    },
    llseek(stream, offset, whence) {
      let position = offset;
      if (whence === 1) position += stream.position;
      else if (whence === 2) position += stream.node.entry.handle.getSize();
      if (position < 0) throw new FS.ErrnoError(EINVAL);
      return position;
    },
  };
  const fileNode = (parent, name, entry) => {
    const node = FS.createNode(parent, name, 0o100666, 0);
    node.node_ops = fileNodeOps;
    node.stream_ops = fileStreamOps;
    node.entry = entry;
    return node;
  };

  const dirNodeOps = {
    getattr: (node) => attr(node, 4096, 1),
    setattr() {},
    lookup(parent, name) {
      const entry = entries.get(name);
      if (!entry?.exists) throw new FS.ErrnoError(ENOENT);
      return fileNode(parent, name, entry);
    },
    mknod(parent, name, mode) {
      const entry = entries.get(name);
      if (!entry || !FS.isFile(mode)) {
        console.error(`[opfs] can't create ${mountPoint}/${name}: only ${[...entries.keys()].join(', ')} live here`);
        throw new FS.ErrnoError(EPERM);
      }
      entry.exists = true;
      return fileNode(parent, name, entry);
    },
    unlink(parent, name) {
      const entry = entries.get(name);
      if (!entry) throw new FS.ErrnoError(ENOENT);
      entry.handle.truncate(0);
      flush(entry);
      entry.exists = false;
    },
    rename() { throw new FS.ErrnoError(EPERM); },
    rmdir() { throw new FS.ErrnoError(EPERM); },
    symlink() { throw new FS.ErrnoError(EPERM); },
    readdir: () => ['.', '..', ...[...entries].filter(([, e]) => e.exists).map(([name]) => name)],
  };

  FS.mkdir(mountPoint);
  FS.mount({
    mount() {
      const node = FS.createNode(null, '/', 0o40777, 0);
      node.node_ops = dirNodeOps;
      node.stream_ops = {};
      return node;
    },
  }, {}, mountPoint);

  return {
    /** Lets go of the files (the tab is going away): flushes and closes every handle. */
    close() {
      for (const entry of entries.values()) {
        try { entry.handle.flush(); entry.handle.close(); } catch { /* already closed */ }
      }
    },
  };
}
