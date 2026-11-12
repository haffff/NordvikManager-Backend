// Drives the browser host's GM page against a real Central: log in, create a game, start its
// session, stop it. node gm-e2e.cjs <AppBundle> <playwright> <port> <centralUrl> <user> <password>
const http = require('http');
const fs = require('fs');
const path = require('path');
const [root, playwrightPath, port, central, user, password] = process.argv.slice(2);
const { chromium } = require(playwrightPath);

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.dat': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.readFile(p.endsWith(path.sep) ? path.join(p, 'index.html') : p, (err, data) => {
    if (err) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { 'Content-Type': mime[path.extname(p)] || 'application/octet-stream' });
    res.end(data);
  });
});

const step = (name, ok, detail) => { console.log(`STEP ${name}: ${ok ? 'PASS' : 'FAIL'} ${detail ?? ''}`); if (!ok) throw new Error(name); };

server.listen(Number(port), async () => {
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await (await browser.newContext()).newPage(); // one context: a second tab shares its storage
  page.on('console', (m) => console.log('[page]', m.text()));
  page.on('pageerror', (e) => console.log('[pageerror]', e.message));
  let failed = false;
  try {
    await page.goto(`http://localhost:${port}/index.html?central=${encodeURIComponent(central)}`);
    await page.waitForSelector('#login-submit', { timeout: 60000 });
    step('page ready', true, await page.textContent('#central'));

    // The games are this tab's (OPFS handles held by its worker): a second tab is refused.
    const other = await page.context().newPage();
    await other.goto(`http://localhost:${port}/index.html?central=${encodeURIComponent(central)}`);
    const refused = await other.waitForFunction(() => /another tab/.test(document.querySelector('#status').textContent), null, { timeout: 30000 })
      .then(() => true, () => false);
    step('a second tab is refused while this one hosts', refused, await other.textContent('#status'));
    await other.close();

    await page.fill('#username', user);
    await page.fill('#password', password);
    await page.click('#login-submit');
    await page.waitForFunction(() => /Logged in|Wrong/.test(document.querySelector('#status').textContent), null, { timeout: 30000 });
    const login = await page.textContent('#status');
    step('login', login.startsWith('Logged in'), login);

    await page.waitForSelector('#create-submit', { timeout: 30000 });
    const basics = await page.$eval('#create input[name="addon"][value="basics"]', (box) => box.checked).catch(() => null);
    step('Basics offered, ticked, for the new game', basics === true, String(basics));
    await page.fill('#game-name', 'E2E tab game');
    await page.click('#create-submit');
    await page.waitForSelector('li[data-game] .start', { timeout: 30000 });
    step('game created and listed', true, await page.textContent('#status'));

    await page.click('li[data-game] .start');
    await page.waitForFunction(() => /is running|Could not start/.test(document.querySelector('#status').textContent), null, { timeout: 60000 });
    const started = await page.textContent('#status');
    step('session started (signaling authenticated)', started.includes('is running'), started);

    await page.click('li[data-game] .stop');
    await page.waitForFunction(() => /stopped|Could not stop/.test(document.querySelector('#status').textContent), null, { timeout: 30000 });
    const stopped = await page.textContent('#status');
    step('session stopped', stopped.includes('stopped'), stopped);

    // A reload: the old worker's handles are released (or waited for), and the game is still there.
    await page.reload();
    await page.waitForFunction(() => document.querySelector('#login-submit') || /another tab/.test(document.querySelector('#status')?.textContent ?? ''), null, { timeout: 60000 });
    step('after a reload the host opens the games again (not "another tab")', !!(await page.$('#login-submit')), await page.textContent('#status'));
    await page.fill('#username', user);
    await page.fill('#password', password);
    await page.click('#login-submit');
    await page.waitForSelector('li[data-game] .name', { timeout: 30000 });
    const names = await page.$$eval('li[data-game] .name', (n) => n.map((x) => x.textContent));
    step('the game is kept across the reload (OPFS)', names.includes('E2E tab game'), names.join(', '));

    // Games from before OPFS: a copy of data.db in IndexedDB ("nordvik-host", store "files").
    // A browser with only that copy gets it moved to OPFS, and the copy deleted once it opened.
    const saved = await page.evaluate(async () => {
      const file = await (await (await navigator.storage.getDirectory()).getFileHandle('data.db')).getFile();
      return Array.from(new Uint8Array(await file.arrayBuffer()));
    });
    const legacy = await (await browser.newContext()).newPage();
    legacy.on('console', (m) => console.log('[legacy]', m.text()));
    await legacy.goto(`http://localhost:${port}/opfs-fs.js`); // this origin, without starting the host
    await legacy.evaluate((bytes) => new Promise((resolve, reject) => {
      const open = indexedDB.open('nordvik-host', 1);
      open.onupgradeneeded = () => open.result.createObjectStore('files');
      open.onsuccess = () => {
        const tx = open.result.transaction('files', 'readwrite');
        tx.objectStore('files').put(new Uint8Array(bytes).buffer, 'data.db');
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
      };
    }), saved);
    await legacy.goto(`http://localhost:${port}/index.html?central=${encodeURIComponent(central)}`);
    await legacy.waitForSelector('#login-submit', { timeout: 60000 });
    await legacy.fill('#username', user);
    await legacy.fill('#password', password);
    await legacy.click('#login-submit');
    await legacy.waitForSelector('li[data-game] .name', { timeout: 30000 });
    const movedNames = await legacy.$$eval('li[data-game] .name', (n) => n.map((x) => x.textContent));
    step('games kept in IndexedDB by an older host are moved to OPFS', movedNames.includes('E2E tab game'), movedNames.join(', '));
    const leftInIndexedDb = await legacy.evaluate(() => new Promise((resolve) => {
      const open = indexedDB.open('nordvik-host', 1);
      open.onsuccess = () => {
        const get = open.result.transaction('files').objectStore('files').get('data.db');
        get.onsuccess = () => resolve(!!get.result);
      };
    }));
    step('the IndexedDB copy is deleted once they opened', !leftInIndexedDb, String(leftInIndexedDb));
  } catch (e) {
    failed = true;
    console.log('RESULT: FAIL ' + e.message);
  }
  if (!failed) console.log('RESULT: PASS');
  await browser.close();
  server.close();
  process.exit(failed ? 1 : 0);
});
