// The browser host's database file in IndexedDB (JsBrowserFileStore.cs). Bytes cross to and from
// C# as memory views (copied once each way), not as base64: a game's database holds its images.
const DB = "nordvik-host";
const STORE = "files";

let loaded = null;  // what load() read, until copyLoaded() hands it to C#
let staged = null;  // what stage() took from C#, until commit() writes it

function open() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function run(mode, action) {
    return open().then((db) => new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, mode);
        const request = action(tx.objectStore(STORE));
        tx.oncomplete = () => { db.close(); resolve(request.result); };
        tx.onerror = () => { db.close(); reject(tx.error); };
        tx.onabort = () => { db.close(); reject(tx.error); };
    }));
}

// The saved file's length (0 when there is none); copyLoaded then copies it out.
export async function load(key) {
    const value = await run("readonly", (store) => store.get(key));
    loaded = value ? new Uint8Array(value) : null;
    return loaded ? loaded.length : 0;
}

export function copyLoaded(view) {
    view.set(loaded);
    loaded = null;
}

export function stage(view) {
    staged = view.slice();
}

export async function commit(key) {
    const contents = staged;
    staged = null;
    await run("readwrite", (store) => store.put(contents.buffer, key));
}
